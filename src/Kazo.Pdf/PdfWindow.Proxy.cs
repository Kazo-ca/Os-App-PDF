using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Kazo.Pdf.Hosting;

namespace Kazo.Pdf;

/// <summary>
/// What the window does with the module's proxy (doc/en/plan.md, work item 1): PdfCraft's file
/// pickers, the chords the window keeps, the user's PdfCraft settings, and the OS theme and language.
/// </summary>
/// <remarks>
/// The proxy's messages arrive through a <c>[JSInvokable]</c> callback, outside the scope the OS opens
/// for app code: each handler that reaches the SDK (files, launching) enters the app's context itself.
/// </remarks>
public sealed partial class PdfWindow
{
    /// <summary>
    /// How long PdfCraft's settings writes are gathered before the file is written: counted from
    /// the first unsaved write, so a PdfCraft that keeps writing still reaches the file.
    /// </summary>
    public static readonly TimeSpan SettingsWriteDelay = TimeSpan.FromMilliseconds(500);

    private WebStorageFile? _settingsFile;
    private CancellationTokenSource? _settingsWrite;

    // Reads the user's PdfCraft settings (eframe's localStorage keys) before the iframe starts:
    // PdfCraft reads them as it starts. They live in ~/.config/ca.kazo.pdf/.
    private async Task<IReadOnlyDictionary<string, string>?> LoadSettingsAsync()
    {
        _settingsFile ??= new WebStorageFile(AppContext.HomeDirectory, AppId);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            return new Dictionary<string, string>(await _settingsFile.LoadAsync(AppContext.CancellationToken), StringComparer.Ordinal);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // The proxy's events of one frame (subscribed in StartFrameAsync, removed in StopFrameAsync).
    private void ConnectProxy(CraftFrame frame, bool connect)
    {
        if (connect)
        {
            frame.KeyPressed += OnKeyPressedAsync;
            frame.PickRequested += OnPickRequestedAsync;
            frame.SettingChanged += OnSettingChangedAsync;
            frame.SchemeChanged += OnSchemeChangedAsync;
        }
        else
        {
            frame.KeyPressed -= OnKeyPressedAsync;
            frame.PickRequested -= OnPickRequestedAsync;
            frame.SettingChanged -= OnSettingChangedAsync;
            frame.SchemeChanged -= OnSchemeChangedAsync;
        }
    }

    // PdfCraft is listening: it takes the OS theme and the user's language, both through ui.set
    // (decision 7). A refused command leaves PdfCraft as it was: the document matters more.
    private async Task ApplyThemeAndLanguageAsync(CraftFrame frame)
    {
        await OnSchemeChangedAsync(await frame.GetColorSchemeAsync());
        await frame.RequestAsync(
            "ui.set",
            new { key = "language", value = PdfCraftLanguage.Pick(AppContext.Culture) },
            cancellationToken: AppContext.CancellationToken);
    }

    private async Task OnSchemeChangedAsync(string scheme)
    {
        if (_frame is { } frame)
        {
            await frame.RequestAsync(
                "ui.set",
                new { key = "theme", value = scheme == "light" ? "light" : "dark" },
                cancellationToken: AppContext.CancellationToken);
        }
    }

    // Ctrl+N: a new Kazo Pdf window.
    private Task OnKeyPressedAsync(ReservedKey key) => PdfKeys.ActionOf(key) switch
    {
        KeyAction.NewWindow => InvokeAsync(LaunchNewWindowAsync),
        _ => Task.CompletedTask,
    };

    // A new, empty Kazo Pdf window.
    private async Task LaunchNewWindowAsync()
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await KazoApps.LaunchAsync(AppId, [], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or UnauthorizedAccessException)
        {
            await ShowProblemAsync(L["NewWindowFailed", exception.Message]);
        }
    }

    // PdfCraft opened its file picker: the OS dialog instead, and the chosen files go back to
    // PdfCraft, whatever the picker was for (File › Open, Combine, Insert pages, an image, a font,
    // a digital ID…): PdfCraft's pickers cannot be told apart by their file types, and every one of
    // them reads the bytes itself. A document PdfCraft opens this way is a new tab of this window.
    // A single PDF chosen is bound to its file when its name is free, so Save writes it back
    // (decision 8); a name already bound to another file is left alone, so a PDF picked to insert
    // its pages never captures the save of a document that has the same name.
    private Task OnPickRequestedAsync(PickRequest request) => InvokeAsync(async () =>
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CraftFrame? frame = _frame;
        if (frame is null)
        {
            return;
        }

        IReadOnlyList<PickedFile>? files = null;
        try
        {
            OpenFileDialogResult choice = await OpenFileAsync(
                new OpenFileDialogRequest
                {
                    Title = L["PickTitle"],
                    InitialDirectory = VirtualPath.Parse(AppContext.HomeDirectory),
                    AllowMultiple = request.Multiple,
                    Filters = FiltersOf(request),
                },
                AppContext.CancellationToken);
            if (choice.Status == FileDialogStatus.Selected && choice.Resources.Count > 0)
            {
                files = await ReadPickedAsync(choice.Resources);
                if (files is not null && choice.Resources.Count == 1)
                {
                    BindPicked(choice.Resources[0].Path.Value);
                    await UpdateTitleAsync();
                }
            }
        }
        finally
        {
            // PdfCraft waits on its picker until it hears back: it always does, even on a failure.
            await frame.AnswerPickAsync(request.Id, files);
        }
    });

    private async Task<IReadOnlyList<PickedFile>?> ReadPickedAsync(IReadOnlyList<SelectedFileResource> resources)
    {
        List<PickedFile> files = [];
        foreach (SelectedFileResource resource in resources)
        {
            string path = resource.Path.Value;
            string name = KazoPath.GetFileName(path) ?? path;
            try
            {
                byte[] bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
                files.Add(new PickedFile(name, string.Empty, bytes));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await ShowProblemAsync(L["ReadFailed", name, exception.Message]);
                return null;
            }
        }

        return files;
    }

    // The extensions PdfCraft accepts, then every file (PdfCraft checks what it gets).
    private List<FileDialogFilter> FiltersOf(PickRequest request)
    {
        List<FileDialogFilter> filters = [];
        if (request.Extensions.Count > 0)
        {
            filters.Add(new FileDialogFilter(L["PickFilter"], request.Extensions, []));
        }

        filters.Add(new FileDialogFilter(L["AllFiles"], [], []));
        return filters;
    }

    // PdfCraft wrote a setting: the user's file is written a moment later, with every write
    // gathered until then. A write already waiting takes the new value: it saves the file as it
    // is when it runs.
    private Task OnSettingChangedAsync(string key, string? value)
    {
        if (_settingsFile is null || !_settingsFile.Set(key, value) || _settingsWrite is not null)
        {
            return Task.CompletedTask;
        }

        CancellationTokenSource write = CancellationTokenSource.CreateLinkedTokenSource(AppContext.CancellationToken);
        _settingsWrite = write;
        _ = WriteSettingsLaterAsync(write);
        return Task.CompletedTask;
    }

    private async Task WriteSettingsLaterAsync(CancellationTokenSource write)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CancellationToken cancellationToken = write.Token;
        try
        {
            await Task.Delay(SettingsWriteDelay, cancellationToken);
            // From here a new write schedules its own save, since this one may already have read the values.
            if (ReferenceEquals(_settingsWrite, write))
            {
                _settingsWrite = null;
                write.Dispose();
            }

            await _settingsFile!.SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed: FlushSettingsAsync writes what was waiting.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The settings stay in this window's PdfCraft; the next write tries again.
        }
    }

    // Writes what is still waiting when the window closes.
    private async Task FlushSettingsAsync()
    {
        if (_settingsWrite is null || _settingsFile is null)
        {
            return;
        }

        _settingsWrite.Cancel();
        _settingsWrite.Dispose();
        _settingsWrite = null;
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await _settingsFile.SaveAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Closing: nothing more to do.
        }
    }

    private async Task ShowProblemAsync(string message)
    {
        try
        {
            await MessageBox(L["ProblemTitle"], message, MessageBoxType.Ok, MessageBoxIcon.Error, AppContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }
}
