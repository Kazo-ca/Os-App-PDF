using System.Globalization;
using Kazo.Pdf.Hosting;

namespace Kazo.Pdf.Tests;

/// <summary>The language the window gives PdfCraft (<c>ui.set { key: "language" }</c>, decision 7).</summary>
public sealed class PdfCraftLanguageTests
{
    [Theory]
    [InlineData("fr-CA", "fr")]
    [InlineData("fr", "fr")]
    [InlineData("en-US", "en")]
    [InlineData("pt-BR", "pt-br")]
    [InlineData("de-DE", "de")]
    [InlineData("uk-UA", "uk")]
    [InlineData("ja-JP", "ja")]
    [InlineData("ar-EG", "ar")]
    [InlineData("vi-VN", "vi")]
    public void A_language_PdfCraft_has_is_kept(string culture, string expected)
    {
        Assert.Equal(expected, PdfCraftLanguage.Pick(CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("ko-KR")]
    [InlineData("sv-SE")]
    [InlineData("nl-NL")]
    public void Other_languages_and_the_ones_the_web_build_cannot_draw_are_English(string culture)
    {
        Assert.Equal(PdfCraftLanguage.English, PdfCraftLanguage.Pick(CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void The_invariant_culture_is_English()
    {
        Assert.Equal(PdfCraftLanguage.English, PdfCraftLanguage.Pick(CultureInfo.InvariantCulture));
    }
}
