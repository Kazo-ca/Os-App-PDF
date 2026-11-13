using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Kazo.Pdf.Documents;
using Microsoft.JSInterop;

namespace Kazo.Pdf;

/// <summary>
/// Opening, saving and closing (doc/en/plan.md, work item 1): the window reads and writes the files
/// with <see cref="KazoFile"/>, asks the OS dialogs, keeps the title and guards the close. The
/// module's <see cref="DocumentSession"/> decides where each save goes.
/// </summary>
/// <remarks>
/// PdfCraft tells the host <c>saveAs</c> as it is (true for Save As and after Apply redactions), so
/// the session's rule applies as is. Its exports are browser downloads in 0.21.1 and never reach
/// the window (Q1).
/// </remarks>
public sealed partial class PdfWindow : IWindowCloseGuard
{
    /// <summary>The app's id, to start another Kazo Pdf window on a file.</summary>
    private const string AppId = "ca.kazo.pdf";

    /// <summary>The window's name for its app, at the end of its title.</summary>
    private const string AppName = "Kazo Pdf";

    /// <summary>
    /// How long the close guard waits for PdfCraft's save of one document: PdfCraft may first ask
    /// a question of its own (lock the Fill &amp; Sign marks), and a save that never comes must not
    /// hold the window open for good.
    /// </summary>
    private static readonly TimeSpan CloseSaveTimeout = TimeSpan.FromMinutes(2);

    private readonly DocumentSession _session = new(PdfFormats.Formats, AppName);

    // Saves run one at a time: each may show a dialog, and the order is PdfCraft's.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Completed with the outcome of the next save (true = written), for the close guard.
    private TaskCompletionSource<bool>? _nextSave;

    // A message over PdfCraft: a failed write, a cancelled save.
    private string? _notice;
    private bool _noticeIsError;

    /// <summary>Gets the title the OS shows for this window: <c>*name — Kazo Pdf</c>.</summary>
    internal string DocumentTitle => _session.Title(L["Untitled"], _startup.State == FrameState.Running);

    /// <summary>
    /// A single PDF chosen in one of PdfCraft's pickers (decision 8): bound to its file when no
    /// other file has its name yet, so that Save writes it back if PdfCraft opened it as a document.
    /// </summary>
    /// <param name="path">The file chosen.</param>
    private void BindPicked(string path)
    {
        string name = CraftDocumentFormats.FileNameOf(path);
        if (PdfFormats.IsPdf(name) && _session.PathOf(name) is null)
        {
            _session.Opened(name, path);
        }
    }

    /// <summary>
    /// Asks before closing a window with work that is not in a file: PdfCraft lists every document
    /// of its page (<c>ui.state</c>, read by <see cref="PdfDocumentList"/>); "Yes" saves each one
    /// and closes only once every write succeeded.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close request.</param>
    /// <returns><see langword="true"/> to close.</returns>
    public async ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (_frame is not { } frame || _startup.State != FrameState.Running)
        {
            return true;
        }

        // A save still being written must end first: PdfCraft already counts the document as
        // saved once it handed the bytes over, so closing now would lose the write.
        await _saveGate.WaitAsync(cancellationToken);
        _saveGate.Release();

        FrameCommandResult inspect = await frame.RequestAsync("ui.state", cancellationToken: cancellationToken);
        if (!inspect.Ok)
        {
            // PdfCraft does not answer, so it cannot save either: closing without saving is the
            // only choice left, and it is the user's.
            if (!_session.IsDirty && _session.UnsavedNames.Count == 0)
            {
                return true;
            }

            MessageBoxDialogResult discard = await MessageBox(
                L["CloseTitle"], L["CloseUnanswered", _session.CurrentName ?? L["Untitled"]], MessageBoxType.YesNo, cancellationToken);
            return discard.Result == MessageBoxResult.Yes;
        }

        IReadOnlyList<OpenDocument> unsaved = _session.WithUnsavedWork(PdfDocumentList.ListFrom(inspect.Value));
        if (unsaved.Count == 0)
        {
            return true;
        }

        string names = string.Join(", ", unsaved.Select(document => document.Title.Length > 0 ? document.Title : document.Name));
        MessageBoxDialogResult answer = await MessageBox(
            L["CloseTitle"], L["CloseUnsaved", names], MessageBoxType.YesNoCancel, cancellationToken);
        return answer.Result switch
        {
            MessageBoxResult.Yes => await SaveAllAsync(frame, unsaved, cancellationToken),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    // After `ready`: the window's file (after Retry), else the file asked for. PdfCraft starts on
    // its start page with no document, so a window with no file has nothing more to do.
    private async Task PrepareDocumentAsync()
    {
        // Runs after `ready`, a [JSInvokable] callback: the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        string? path = (_session.CurrentName is { } current ? _session.PathOf(current) : null) ?? _request.Path;
        if (path is not null)
        {
            await OpenFileHereAsync(path);
        }

        await UpdateTitleAsync();
    }

    // Reads a file and sends it to PdfCraft; a file that does not read leaves a message.
    private async Task<bool> OpenFileHereAsync(string path)
    {
        if (_frame is not { } frame)
        {
            return false;
        }

        string name = CraftDocumentFormats.FileNameOf(path);
        byte[] bytes;
        try
        {
            bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await ShowNoticeAsync(L["OpenFailed", name, exception.Message], error: true);
            return false;
        }

        if (!await frame.OpenAsync(name, bytes))
        {
            await ShowNoticeAsync(L["OpenFailed", name, L["NotConnected"]], error: true);
            return false;
        }

        // Bound under the file's own name. PdfCraft saves a PDF under that name, which Save then
        // rewrites; an image or a text file becomes a new PDF named after it (scan.png saves as
        // scan.pdf), so Save asks for a path and never writes PDF bytes over the image.
        _session.Opened(name, path);
        await UpdateTitleAsync();
        return true;
    }

    private async Task OnDirtyChangedAsync(bool dirty)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        bool before = _session.IsDirty;
        _session.SetFrameDirty(dirty);
        if (before != _session.IsDirty)
        {
            await UpdateTitleAsync();
        }
    }

    // One file PdfCraft saves: decided here and by the session, written here.
    private async Task<bool> OnSavingAsync(FrameSave save)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        await _saveGate.WaitAsync(AppContext.CancellationToken);
        bool written = false;
        try
        {
            written = await SaveAsync(save);
        }
        finally
        {
            _saveGate.Release();
            Interlocked.Exchange(ref _nextSave, null)?.TrySetResult(written);
            await UpdateTitleAsync();
        }

        return written;
    }

    // One file PdfCraft exports (Export, Print to PDF, Reduce size, OCR, form data…; ADR 0264 of
    // Kazo OS): always a copy in the document's folder, even when it is a .pdf, so the window stays
    // bound to its document. It does not wait for the save gate: it never touches the document.
    private async Task<bool> OnExportingAsync(FrameSave export)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (string.IsNullOrEmpty(export.Name))
        {
            return false;
        }

        SaveDecision decision = _session.DecideCopy(export.Name);
        try
        {
            return await CarryOutAsync(decision, export.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ShowNoticeAsync(L["SaveFailed", export.Name, exception.Message], error: true);
            return false;
        }
    }

    private async Task<bool> SaveAsync(FrameSave save)
    {
        if (string.IsNullOrEmpty(save.Name))
        {
            return false;
        }

        SaveDecision decision = _session.Decide(save.Name, save.SaveAs);
        try
        {
            return await CarryOutAsync(decision, save.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever went wrong (a dialog, the file system), the document must not pass for saved.
            _session.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", save.Name, exception.Message], error: true);
            return false;
        }
    }

    // Asks for the path when the decision says so, writes the bytes, and records the outcome.
    private async Task<bool> CarryOutAsync(SaveDecision decision, byte[] bytes)
    {
        string? path = decision.AsksForPath ? await AskSavePathAsync(decision) : decision.Path;
        if (path is null)
        {
            _session.SaveFailed(decision);
            if (decision.SavesTheDocument)
            {
                await ShowNoticeAsync(L["SaveCancelled", decision.Name], error: false);
            }

            return false;
        }

        try
        {
            await KazoFile.WriteAllBytesAsync(path, bytes, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _session.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", CraftDocumentFormats.FileNameOf(path), exception.Message], error: true);
            return false;
        }

        _session.Saved(decision, path);
        if (decision.Action == SaveAction.AskForCopy)
        {
            await ShowNoticeAsync(L["CopySaved", CraftDocumentFormats.FileNameOf(path)], error: false);
            return true;
        }

        // PdfCraft has no command to rename a document: its tab keeps the old name, and its next
        // save, under that name, goes to the file just chosen (the binding).
        await ClearNoticeAsync();
        return true;
    }

    // The OS save dialog, in the document's folder, for the format PdfCraft chose.
    private async Task<string?> AskSavePathAsync(SaveDecision decision)
    {
        string extension = CraftDocumentFormats.ExtensionOf(decision.Name);
        CraftDocumentFormat? format = PdfFormats.Formats.Find(decision.Name);
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = decision.Action == SaveAction.AskForCopy ? L["SaveCopyTitle"] : L["SaveTitle"],
            InitialDirectory = DialogFolder(decision.Folder),
            SuggestedFileName = decision.Name,
            DefaultExtension = extension.Length > 0 ? extension : null,
            Filters = extension.Length > 0
                ? [new FileDialogFilter(L["FilterFormat", extension], [extension], format is null ? [] : [format.MediaType])]
                : [],
            ConfirmOverwrite = true,
        });
        return result.Status == FileDialogStatus.Selected && result.Resource is { } chosen
            ? CraftDocumentFormats.WithExtensionOf(chosen.Path.Value, decision.Name)
            : null;
    }

    // "Yes" in the close dialog: each document is shown and saved in turn; the window closes only
    // when all of them reached a file.
    private async Task<bool> SaveAllAsync(CraftFrame frame, IReadOnlyList<OpenDocument> documents, CancellationToken cancellationToken)
    {
        foreach (OpenDocument document in documents)
        {
            // PdfCraft's File › Save saves the document it shows: show this one first.
            if (!await ShowDocumentAsync(frame, document.Id, cancellationToken))
            {
                await ShowNoticeAsync(L["SaveFailed", document.Title, L["NotShown"]], error: true);
                return false;
            }

            TaskCompletionSource<bool> outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSave = outcome;
            FrameCommandResult started = await frame.RequestAsync("ui.command", new { id = "file.save" }, cancellationToken: cancellationToken);
            if (!started.Ok)
            {
                _nextSave = null;
                await ShowNoticeAsync(L["SaveFailed", document.Title, started.Error ?? string.Empty], error: true);
                return false;
            }

            bool written;
            try
            {
                written = await outcome.Task.WaitAsync(CloseSaveTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _nextSave = null;
                written = false;
            }

            if (!written)
            {
                return false;
            }
        }

        return true;
    }

    // PdfCraft has no command that shows a document by its id: step through its tabs
    // (view.next_tab) until ui.state says the document is the one shown, at most once round.
    private static async Task<bool> ShowDocumentAsync(CraftFrame frame, long id, CancellationToken cancellationToken)
    {
        FrameCommandResult state = await frame.RequestAsync("ui.state", cancellationToken: cancellationToken);
        int tabs = state.Ok ? PdfDocumentList.TabCountOf(state.Value) : 0;
        for (int step = 0; state.Ok && step <= tabs; step++)
        {
            if (PdfDocumentList.ActiveOf(state.Value) == id)
            {
                return true;
            }

            FrameCommandResult next = await frame.RequestAsync("ui.command", new { id = "view.next_tab" }, cancellationToken: cancellationToken);
            if (!next.Ok)
            {
                return false;
            }

            state = await frame.RequestAsync("ui.state", cancellationToken: cancellationToken);
        }

        return false;
    }

    private VirtualPath DialogFolder(string? folder) => VirtualPath.Parse(folder ?? AppContext.HomeDirectory);

    private async Task UpdateTitleAsync()
    {
        try
        {
            await SetWindowTitleAsync(DocumentTitle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            // The window is closing.
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowNoticeAsync(string text, bool error)
    {
        _notice = text;
        _noticeIsError = error;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearNoticeAsync()
    {
        if (_notice is not null)
        {
            _notice = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void DismissNotice() => _notice = null;
}
