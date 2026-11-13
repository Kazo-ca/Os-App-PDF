using Kazo.Os.AppSdk.Craft;
using Kazo.Pdf.Hosting;

namespace Kazo.Pdf.Tests;

/// <summary>The chords the window takes from PdfCraft: the OS chords and Ctrl+N.</summary>
public sealed class PdfKeysTests
{
    // What the proxy reports: the key as the browser gives it, the physical code, the modifiers.
    private static ReservedKey Typed(string key, string code, bool ctrl = false, bool shift = false, bool alt = false) =>
        new(key, code, ctrl, alt, shift, Meta: false, ReservedKeyTarget.App);

    [Fact]
    public void The_window_reserves_the_OS_chords_and_new_window_only()
    {
        // PdfCraft says saveAs itself: F12 and Ctrl+Shift+S stay PdfCraft's.
        Assert.Equal(ReservedKeys.All, PdfKeys.All);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("N")]
    public void Ctrl_N_is_a_new_window(string key)
    {
        Assert.Equal(KeyAction.NewWindow, PdfKeys.ActionOf(Typed(key, "KeyN", ctrl: true)));
    }

    [Fact]
    public void Ctrl_N_on_another_layout_is_found_by_its_code()
    {
        Assert.Equal(KeyAction.NewWindow, PdfKeys.ActionOf(Typed("ñ", "KeyN", ctrl: true)));
    }

    [Theory]
    [InlineData("S", "KeyS", true, true)]
    [InlineData("F12", "F12", false, false)]
    [InlineData("n", "KeyN", true, true)]
    [InlineData("n", "KeyN", false, false)]
    public void Other_chords_do_nothing(string key, string code, bool ctrl, bool shift)
    {
        Assert.Equal(KeyAction.None, PdfKeys.ActionOf(Typed(key, code, ctrl, shift)));
    }
}
