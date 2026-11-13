using System.Globalization;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Pdf.Hosting;

/// <summary>
/// The interface language PdfCraft gets from the window (<c>ui.set { key: "language" }</c>): the
/// Kazo user's language when PdfCraft has it, otherwise English (doc/en/plan.md, decision 7).
/// </summary>
/// <remarks>
/// The choice is the module's <see cref="CraftLanguage.Pick"/>: the regional code PdfCraft lists
/// (<c>pt-br</c>), else the two-letter one. PdfCraft 0.21.1 has 26 languages (<c>LANGUAGES</c> in
/// its <c>i18n.rs</c>). Simplified and traditional Chinese stay English: the web build has no
/// Chinese font, so their labels would not draw (PdfCraft's own gap 5).
/// </remarks>
public static class PdfCraftLanguage
{
    /// <summary>The language PdfCraft falls back to.</summary>
    public const string English = CraftLanguage.English;

    /// <summary>Gets the codes <c>ui.set language</c> takes, without the ones the web build cannot draw.</summary>
    public static IReadOnlyList<string> Supported { get; } =
    [
        "en", "ja", "cs", "pt-br", "de", "es", "fr", "ru", "kk", "bg", "te", "hu", "uk", "it",
        "ar", "ckb", "fa", "pl", "sl", "el", "tr", "lt", "id", "vi",
    ];

    /// <summary>The PdfCraft code for a culture.</summary>
    /// <param name="culture">The Kazo user's culture.</param>
    /// <returns>A code of <see cref="Supported"/>.</returns>
    public static string Pick(CultureInfo culture) => CraftLanguage.Pick(culture, Supported);
}
