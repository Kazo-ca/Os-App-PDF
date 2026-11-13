using Kazo.Os.AppSdk.Craft;
using Kazo.Pdf.Documents;

namespace Kazo.Pdf.Tests;

/// <summary>The formats of Q3 and how a save is decided for them (doc/en/plan.md).</summary>
public sealed class PdfFormatsTests
{
    [Fact]
    public void The_Open_dialog_proposes_PDF()
    {
        Assert.Equal([".pdf"], PdfFormats.OpenExtensions);
    }

    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData("REPORT.PDF", true)]
    [InlineData("scan.png", false)]
    [InlineData("notes.txt", false)]
    [InlineData("pdf", false)]
    public void A_PDF_is_told_by_its_extension(string name, bool expected)
    {
        Assert.Equal(expected, PdfFormats.IsPdf(name));
    }

    [Fact]
    public void No_format_is_a_copy()
    {
        // PdfCraft 0.21.1 hands only documents to the host; its exports are browser downloads (Q1).
        Assert.DoesNotContain(PdfFormats.Formats.All, format => format.IsCopy);
    }

    [Fact]
    public void A_PDF_opened_from_a_file_is_saved_back_to_it()
    {
        DocumentSession session = new(PdfFormats.Formats, "Kazo Pdf");
        session.Opened("report.pdf", "/home/user/report.pdf");

        SaveDecision decision = session.Decide("report.pdf", saveAs: false);

        Assert.Equal(SaveAction.Rewrite, decision.Action);
        Assert.Equal("/home/user/report.pdf", decision.Path);
    }

    [Fact]
    public void Save_As_always_asks()
    {
        DocumentSession session = new(PdfFormats.Formats, "Kazo Pdf");
        session.Opened("report.pdf", "/home/user/report.pdf");

        Assert.Equal(SaveAction.AskAndBind, session.Decide("report.pdf", saveAs: true).Action);
    }

    [Fact]
    public void An_image_opened_as_a_new_PDF_is_never_overwritten()
    {
        // scan.png opens as a new PDF that PdfCraft saves as scan.pdf: not the bound name.
        DocumentSession session = new(PdfFormats.Formats, "Kazo Pdf");
        session.Opened("scan.png", "/home/user/scan.png");

        Assert.Equal(SaveAction.AskAndBind, session.Decide("scan.pdf", saveAs: false).Action);
    }

    [Fact]
    public void A_redacted_copy_asks_where_to_go()
    {
        // After Apply redactions PdfCraft proposes report_Redacted.pdf with saveAs true.
        DocumentSession session = new(PdfFormats.Formats, "Kazo Pdf");
        session.Opened("report.pdf", "/home/user/report.pdf");

        SaveDecision decision = session.Decide("report_Redacted.pdf", saveAs: true);

        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Equal("/home/user", decision.Folder);
    }
}
