using Kazo.Os.App.Abstractions;

namespace Kazo.Pdf.Tests;

/// <summary>The app's manifest class, the source of the generated manifest.json (doc/en/plan.md, decision 1).</summary>
public sealed class ManifestTests
{
    private readonly PdfApp _app = new();

    [Fact]
    public void The_app_is_the_Kazo_Pdf_window_app()
    {
        Assert.Equal("ca.kazo.pdf", _app.Id);
        Assert.Equal("Kazo Pdf", _app.Name);
        Assert.Equal("pub.kazo", _app.PublisherId);
        Assert.Equal("productivity", _app.Category);
        Assert.Equal(AppKind.Window, _app.Kind);
        Assert.Equal(typeof(PdfWindow), _app.EntryPoint);
    }

    [Fact]
    public void Several_windows_can_run_at_once()
    {
        Assert.False(_app.SingleInstancePerUser);
    }

    [Fact]
    public void The_window_opens_at_1200_by_820_and_keeps_PdfCraft_minimum()
    {
        WindowOptions window = Assert.IsType<WindowOptions>(_app.Window);
        Assert.Equal(1200, window.DefaultWidth);
        Assert.Equal(820, window.DefaultHeight);
        Assert.Equal(482, window.MinWidth);
        Assert.Equal(560, window.MinHeight);
    }

    [Fact]
    public void The_window_stays_on_its_screen()
    {
        // Kazo OS ADR 0263, issue #2: another screen would reload PdfCraft's iframe.
        WindowOptions window = Assert.IsType<WindowOptions>(_app.Window);
        Assert.False(window.CanChangeScreen);
    }

    [Fact]
    public void The_icon_is_declared_as_the_app_icon()
    {
        Assert.Equal(["icons/app.svg"], _app.Icons);
        AssetManifest icon = Assert.Single(_app.Assets);
        Assert.Equal("icons/app.svg", icon.Path);
        Assert.Equal("app-icon", icon.Role);
    }

    [Fact]
    public void The_window_asks_for_the_dialogs_the_files_and_apps_launch()
    {
        Assert.Equal(
            [
                AppCapabilities.DialogFileOpen,
                AppCapabilities.DialogFileSave,
                AppCapabilities.FileSystemUserHomeRead,
                AppCapabilities.FileSystemUserHomeWrite,
                AppCapabilities.FileSystemPrivateRead,
                AppCapabilities.FileSystemPrivateWrite,
                AppCapabilities.AppsLaunch,
            ],
            _app.Capabilities);
    }

    [Fact]
    public void Kazo_Pdf_is_the_default_app_of_PDF()
    {
        FileHandler handler = Assert.Single(_app.FileHandlers, candidate => candidate.Extensions.Contains(".pdf"));
        Assert.Equal("application/pdf", handler.MediaType);
        Assert.Contains("open", handler.Actions);
        Assert.Contains("edit", handler.Actions);
    }

    [Fact]
    public void Only_PDF_is_associated()
    {
        // Q3 = B: the OS viewers and editor stay the only apps of images and text files.
        FileHandler handler = Assert.Single(_app.FileHandlers);
        Assert.Equal([".pdf"], handler.Extensions);
    }
}
