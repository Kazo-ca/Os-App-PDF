using System.Text.Json;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Pdf.Documents;

/// <summary>
/// Reads the documents PdfCraft has open from its <c>ui.state</c> answer (doc/en/plan.md,
/// decision 6): <c>documents[{ tab, doc, name, path, pages, dirty, active }]</c>.
/// </summary>
/// <remarks>
/// PdfCraft's <c>ui.inspect</c> is its accessibility tree, not a document list, so the module's
/// <see cref="OpenDocument.ListFrom(JsonElement, Func{string, string?})"/> (WordCraft's and
/// GridCraft's shapes) finds nothing in it. Until the module reads this third shape, the window
/// reads it here. The answer comes from the iframe: anything malformed is skipped.
/// </remarks>
public static class PdfDocumentList
{
    /// <summary>Reads the documents of a <c>ui.state</c> answer.</summary>
    /// <param name="state">PdfCraft's answer.</param>
    /// <returns>
    /// One entry per document (a document shown in two tabs, a split view, is listed once, active
    /// when one of its tabs is), in PdfCraft's tab order; empty when the answer has no document list.
    /// <see cref="OpenDocument.Name"/> is the name PdfCraft saves the document under: its
    /// <c>name</c> (the web build has no <c>path</c>).
    /// </returns>
    public static IReadOnlyList<OpenDocument> ListFrom(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object
            || !state.TryGetProperty("documents", out JsonElement documents)
            || documents.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<OpenDocument> list = [];
        foreach (JsonElement document in documents.EnumerateArray())
        {
            if (document.ValueKind != JsonValueKind.Object
                || !document.TryGetProperty("doc", out JsonElement id)
                || id.ValueKind != JsonValueKind.Number
                || !id.TryGetInt64(out long doc))
            {
                continue;
            }

            bool active = Flag(document, "active");
            int listed = list.FindIndex(entry => entry.Id == doc);
            if (listed >= 0)
            {
                list[listed] = list[listed] with { Active = list[listed].Active || active };
                continue;
            }

            string name = Text(document, "name") ?? string.Empty;
            list.Add(new OpenDocument(doc, Text(document, "path") ?? (name.Length > 0 ? name : null), name, Flag(document, "dirty"), active));
        }

        return list;
    }

    /// <summary>The document PdfCraft shows, from a <c>ui.state</c> answer.</summary>
    /// <param name="state">PdfCraft's answer.</param>
    /// <returns>Its id, or <see langword="null"/> when no document is shown.</returns>
    public static long? ActiveOf(JsonElement state) =>
        ListFrom(state).FirstOrDefault(document => document.Active)?.Id;

    /// <summary>How many tabs PdfCraft shows, from a <c>ui.state</c> answer (a split view counts twice).</summary>
    /// <param name="state">PdfCraft's answer.</param>
    /// <returns>The number of entries of <c>documents</c>; 0 when there is no list.</returns>
    public static int TabCountOf(JsonElement state) =>
        state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty("documents", out JsonElement documents)
        && documents.ValueKind == JsonValueKind.Array
            ? documents.GetArrayLength()
            : 0;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
