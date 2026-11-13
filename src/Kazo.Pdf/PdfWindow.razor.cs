using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.Globalization;
using Kazo.Pdf.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Kazo.Pdf;

/// <summary>
/// The Kazo Pdf window (doc/en/plan.md, work item 1): PdfCraft in an iframe started by the
/// Kazo OS module <c>Kazo.Os.AppSdk.Craft</c> (<see cref="CraftFrame"/>), with a loading screen
/// until it is ready and an error screen with "Retry" when it fails or does not answer within
/// <see cref="FrameStartup.Timeout"/>.
/// </summary>
/// <remarks>
/// Opening, saving and the close guard are in <c>PdfWindow.Documents.cs</c>; the file picker,
/// chords, settings, theme and language in <c>PdfWindow.Proxy.cs</c>. PdfCraft 0.21.1 renders with
/// WebGL 2 only (eframe's Glow renderer): there is no WebGPU switch.
/// </remarks>
public sealed partial class PdfWindow : IAsyncDisposable
{
    private readonly FrameStartup _startup = new();
    private ElementReference _frameHost;
    private CraftFrame? _frame;
    private CancellationTokenSource? _timeout;
    private IStringLocalizer<Strings>? _localizer;
    private OpenRequest _request = new(null, false);

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    private IStringLocalizer<Strings> L => _localizer ??= AppContext.GetLocalizer<Strings>();

    /// <summary>The error screen's detail: PdfCraft's own error, or the timeout.</summary>
    private string FailureText => _startup.TimedOut
        ? L["TimedOut", (int)FrameStartup.Timeout.TotalSeconds]
        : _startup.Error ?? string.Empty;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        CancelTimeout();
        await StopFrameAsync();
        await FlushSettingsAsync();
        _nextSave?.TrySetResult(false);
        _saveGate.Dispose();
    }

    /// <inheritdoc />
    /// <remarks>Reads what the window was started for, and names it before it is first shown.</remarks>
    protected override async Task OnAppInitializedAsync()
    {
        _request = OpenRequest.FromArguments(LaunchArguments);
        await SetWindowTitleAsync(DocumentTitle);
    }

    /// <inheritdoc />
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await StartFrameAsync();
        }
    }

    /// <inheritdoc />
    /// <remarks>The window came to the front: the keyboard goes to PdfCraft.</remarks>
    protected override async Task OnWindowActivatedAsync()
    {
        if (_frame is not null && _startup.State == FrameState.Running)
        {
            await _frame.FocusAsync();
        }
    }

    private async Task StartFrameAsync()
    {
        CraftFrame frame = new(JavaScript);
        frame.Ready += OnReadyAsync;
        frame.Failed += OnFailedAsync;
        frame.DirtyChanged += OnDirtyChangedAsync;
        frame.Saving += OnSavingAsync;
        ConnectProxy(frame, connect: true);
        _frame = frame;
        ArmTimeout(_startup.Attempt);

        try
        {
            await frame.StartAsync(_frameHost, await FrameOptionsAsync(), AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            await OnFailedAsync(exception.Message);
        }
    }

    // PdfCraft's page, its language, the page API, the user's name for comments, the chords the
    // window keeps, and the user's PdfCraft settings.
    private async Task<CraftFrameOptions> FrameOptionsAsync()
    {
        // "control" (bare): PdfCraft installs window.pdfcraft, the page API the window's commands
        // go through, only when the page address has it (doc/en/plan.md, step 0).
        Dictionary<string, string> parameters = new(StringComparer.Ordinal) { ["control"] = string.Empty };
        if (!string.IsNullOrWhiteSpace(AppContext.UserDisplayName))
        {
            parameters["author"] = AppContext.UserDisplayName;
        }

        return new CraftFrameOptions(CraftFrameOptions.PagePathOf(typeof(PdfWindow).Assembly.GetName().Name!), L["EditorLabel"])
        {
            Language = PdfCraftLanguage.Pick(AppContext.Culture),
            Parameters = parameters,
            ReservedKeys = PdfKeys.All,
            Settings = await LoadSettingsAsync(),
        };
    }

    private async Task StopFrameAsync()
    {
        if (_frame is null)
        {
            return;
        }

        _frame.Ready -= OnReadyAsync;
        _frame.Failed -= OnFailedAsync;
        _frame.DirtyChanged -= OnDirtyChangedAsync;
        _frame.Saving -= OnSavingAsync;
        ConnectProxy(_frame, connect: false);
        await _frame.DisposeAsync();
        _frame = null;
    }

    private async Task RetryAsync()
    {
        CancelTimeout();
        await StopFrameAsync();
        _startup.Retry();
        StateHasChanged();
        await StartFrameAsync();
    }

    private async Task OnReadyAsync(string? appVersion)
    {
        if (!_startup.OnReady(appVersion))
        {
            return;
        }

        CancelTimeout();
        await InvokeAsync(StateHasChanged);
        if (_frame is { } frame)
        {
            await ApplyThemeAndLanguageAsync(frame);
        }

        await PrepareDocumentAsync();
        if (_frame is { } ready && IsWindowActive)
        {
            await ready.FocusAsync();
        }
    }

    private async Task OnFailedAsync(string? error)
    {
        if (_startup.OnFailed(error))
        {
            CancelTimeout();
            await InvokeAsync(StateHasChanged);
        }
    }

    // Shows the error screen if attempt `attempt` gets no answer in time.
    private void ArmTimeout(int attempt)
    {
        CancelTimeout();
        CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(AppContext.CancellationToken);
        _timeout = timeout;
        _ = WaitForTimeoutAsync(attempt, timeout.Token);
    }

    private async Task WaitForTimeoutAsync(int attempt, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FrameStartup.Timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            if (_startup.OnTimeout(attempt))
            {
                StateHasChanged();
            }
        });
    }

    private void CancelTimeout()
    {
        _timeout?.Cancel();
        _timeout?.Dispose();
        _timeout = null;
    }
}
