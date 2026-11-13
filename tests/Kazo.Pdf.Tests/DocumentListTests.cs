using System.Text.Json;
using Kazo.Os.AppSdk.Craft;
using Kazo.Pdf.Documents;

namespace Kazo.Pdf.Tests;

/// <summary>PdfCraft's <c>ui.state</c> answer, read for the close guard (doc/en/plan.md, decision 6).</summary>
public sealed class DocumentListTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void Each_document_is_listed_once_with_its_id_name_and_state()
    {
        // A split view shows document 4 in two tabs; the second one is active.
        JsonElement state = Json("""
            {
              "documents": [
                { "tab": 0, "doc": 3, "name": "report.pdf", "path": null, "pages": 4, "dirty": false, "active": false },
                { "tab": 1, "doc": 4, "name": "scan.pdf", "path": null, "pages": 1, "dirty": true, "active": false },
                { "tab": 2, "doc": 4, "name": "scan.pdf", "path": null, "pages": 1, "dirty": true, "active": true }
              ],
              "active": { "doc": 4, "page": 1 }
            }
            """);

        Assert.Equal(
            [
                new OpenDocument(3, "report.pdf", "report.pdf", Dirty: false, Active: false),
                new OpenDocument(4, "scan.pdf", "scan.pdf", Dirty: true, Active: true),
            ],
            PdfDocumentList.ListFrom(state));
        Assert.Equal(4, PdfDocumentList.ActiveOf(state));
        Assert.Equal(3, PdfDocumentList.TabCountOf(state));
    }

    [Fact]
    public void The_module_reader_finds_nothing_in_this_shape()
    {
        // Why the window reads ui.state itself: no "id", no "windows" (WordCraft's and GridCraft's shapes).
        JsonElement state = Json("""{ "documents": [ { "tab": 0, "doc": 3, "name": "a.pdf", "dirty": true, "active": true } ] }""");

        Assert.Empty(OpenDocument.ListFrom(state));
        Assert.Single(PdfDocumentList.ListFrom(state));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "documents": 3 }""")]
    public void An_answer_without_a_document_list_lists_nothing(string text)
    {
        JsonElement state = Json(text);

        Assert.Empty(PdfDocumentList.ListFrom(state));
        Assert.Null(PdfDocumentList.ActiveOf(state));
        Assert.Equal(0, PdfDocumentList.TabCountOf(state));
    }

    [Fact]
    public void Malformed_entries_are_skipped()
    {
        JsonElement state = Json("""
            {
              "documents": [
                "report.pdf",
                { "doc": "3", "name": "text id.pdf" },
                { "doc": 1.5, "name": "fraction.pdf" },
                { "name": "no id.pdf", "dirty": true },
                { "doc": 8, "name": 12, "dirty": "yes", "active": 1 },
                { "doc": 9, "name": "kept.pdf", "dirty": true }
              ]
            }
            """);

        Assert.Equal(
            [
                new OpenDocument(8, null, string.Empty, Dirty: false, Active: false),
                new OpenDocument(9, "kept.pdf", "kept.pdf", Dirty: true, Active: false),
            ],
            PdfDocumentList.ListFrom(state));
    }

    [Fact]
    public void A_path_when_given_is_the_name()
    {
        JsonElement state = Json("""{ "documents": [ { "doc": 2, "name": "a.pdf", "path": "/x/a.pdf", "dirty": false } ] }""");

        Assert.Equal("/x/a.pdf", Assert.Single(PdfDocumentList.ListFrom(state)).Name);
    }
}
