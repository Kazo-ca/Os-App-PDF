using Kazo.Os.AppSdk.Craft;

namespace Kazo.Pdf.Hosting;

/// <summary>
/// The chords the iframe gives back to the window instead of PdfCraft: the OS chords and Ctrl+N
/// for a new window (<see cref="ReservedKeys.All"/>).
/// </summary>
/// <remarks>
/// Unlike GridCraft, PdfCraft tells the host <c>saveAs: true</c> for its Save As, so F12 and
/// Ctrl+Shift+S stay PdfCraft's.
/// </remarks>
public static class PdfKeys
{
    /// <summary>Gets every chord the window reserves.</summary>
    public static IReadOnlyList<ReservedKey> All { get; } = ReservedKeys.All;

    /// <summary>
    /// Reads a typed chord. The proxy reports the key as the browser gave it, so the comparison
    /// ignores the case of the key and takes the physical code when it matches.
    /// </summary>
    /// <param name="pressed">The chord the proxy reported.</param>
    /// <returns>What the window does.</returns>
    public static KeyAction ActionOf(ReservedKey pressed)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        return Matches(pressed, ReservedKeys.NewWindow) ? KeyAction.NewWindow : KeyAction.None;
    }

    private static bool Matches(ReservedKey pressed, ReservedKey chord)
    {
        if (pressed.Ctrl != chord.Ctrl || pressed.Alt != chord.Alt || pressed.Shift != chord.Shift || pressed.Meta != chord.Meta)
        {
            return false;
        }

        bool sameKey = pressed.Key is { } key && chord.Key is { } wanted
            && string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase);
        bool sameCode = pressed.Code is { } code && chord.Code is { } wantedCode
            && string.Equals(code, wantedCode, StringComparison.Ordinal);
        return sameKey || sameCode;
    }
}

/// <summary>What a chord typed in PdfCraft asks the window for.</summary>
public enum KeyAction
{
    /// <summary>Nothing the window knows.</summary>
    None,

    /// <summary>A new, empty Kazo Pdf window.</summary>
    NewWindow,
}
