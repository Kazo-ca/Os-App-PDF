using Kazo.Os.App.Abstractions;
using Kazo.Pdf.Documents;

namespace Kazo.Pdf;

/// <summary>
/// The app's manifest (doc/en/plan.md, decision 1). The build generates <c>manifest.json</c> from
/// this class (Kazo.Os.App.Build); it is never instantiated at run time.
/// </summary>
/// <remarks>
/// Identity and window (decision 1), formats (Q3, <see cref="PdfFormats"/>), the OS dialogs and the
/// capabilities they need, with the window on the Kazo.Os.AppSdk.Craft module (decision 2), and a
/// window that stays on its screen (decision 3).
/// </remarks>
[AppManifest]
public sealed class PdfApp : AppDefinition
{
    /// <summary>Smallest usable width of PdfCraft, in pixels (its desktop <c>MIN_WINDOW_SIZE</c>).</summary>
    public const int ContentMinWidth = 480;

    /// <summary>Smallest usable height of PdfCraft, in pixels (its desktop <c>MIN_WINDOW_SIZE</c>).</summary>
    public const int ContentMinHeight = 520;

    /// <inheritdoc />
    public override string Id => "ca.kazo.pdf";

    /// <inheritdoc />
    public override string Name => "Kazo Pdf";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(PdfWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "A PDF reader and editor, built on PdfCraft";

    /// <inheritdoc />
    public override string Category => "productivity";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Kazo OS Store must be v + this version.</remarks>
    public override string Version => "0.1.0";

    /// <inheritdoc />
    /// <remarks>
    /// Several windows at once: each one runs its own PdfCraft in its own iframe, because the
    /// PdfCraft canvas id (<c>pdfcraft</c>) is fixed and allows one instance per HTML document
    /// (doc/en/plan.md).
    /// </remarks>
    public override bool SingleInstancePerUser => false;

    /// <inheritdoc />
    /// <remarks>
    /// The OS Open and Save dialogs, the user's files (a file chosen in a dialog outside the home
    /// folder is reached through the dialog's grant), <c>apps.launch</c> to open a file in another
    /// window (Ctrl+N, a file opened from the OS while the window has work), and the private folder
    /// where PdfCraft's settings live (<c>~/.config/ca.kazo.pdf/</c>).
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.DialogFileOpen,
        AppCapabilities.DialogFileSave,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.FileSystemPrivateRead,
        AppCapabilities.FileSystemPrivateWrite,
        AppCapabilities.AppsLaunch,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// PDF with <c>open</c> and <c>edit</c> (Kazo Pdf becomes its app); images and text with
    /// <c>open</c> only, for "Open with" (Q3 = A).
    /// </remarks>
    public override IReadOnlyList<FileHandler> FileHandlers => PdfFormats.FileHandlers;

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    /// <remarks>
    /// Sizes are the outer size of the OS window (title bar included): 1200 × 820 by default, room
    /// for PdfCraft's tool bar, a side panel and a page; at least PdfCraft's minimum plus the frame.
    /// <c>CanChangeScreen = false</c> (Kazo OS ADR 0263, issue #2): moving the window to another
    /// screen would reload PdfCraft's iframe and lose the open documents.
    /// </remarks>
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 1200,
        DefaultHeight = 820,
        MinWidth = ContentMinWidth + WindowChromeWidth,
        MinHeight = ContentMinHeight + WindowChromeHeight,
        CanChangeScreen = false,
    };

    // Borders and title bar of an OS window (the tallest title bar of the Kazo OS themes, 2.35rem).
    private const int WindowChromeWidth = 2;
    private const int WindowChromeHeight = 40;
}
