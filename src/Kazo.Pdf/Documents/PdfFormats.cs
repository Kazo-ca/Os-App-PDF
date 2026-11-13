using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Pdf.Documents;

/// <summary>
/// The files Kazo Pdf is associated with (doc/en/plan.md, Q3 = B): PDF only. Images and text files
/// still open from PdfCraft's own picker ("All files" in the OS dialog), as a new, unsaved PDF.
/// </summary>
/// <remarks>
/// PdfCraft saves a document as <c>.pdf</c> only. An image or a text file picked in the window
/// opens as a new PDF named after it (<c>scan.png</c> becomes <c>scan.pdf</c>): its save carries
/// another name than the file's, so the session asks for a path and never writes PDF bytes over
/// the image. PdfCraft's exports (Export, Print to PDF…) reach the window apart from its saves
/// (<c>CraftFrame.Exporting</c>, Q1) and are always copies, so no format here is a copy.
/// </remarks>
public static class PdfFormats
{
    /// <summary>The extension of the documents PdfCraft saves.</summary>
    public const string DocumentExtension = ".pdf";

    /// <summary>Gets every format, with what Kazo Pdf does with it.</summary>
    public static CraftDocumentFormats Formats { get; } = new(
    [
        // Default app (open and edit).
        new(".pdf", "application/pdf", IsDefault: true, IsCopy: false),

    ]);

    /// <summary>Gets the extensions the OS Open dialog proposes, in the order above.</summary>
    public static IReadOnlyList<string> OpenExtensions => Formats.OpenExtensions;

    /// <summary>Gets the file associations of the manifest.</summary>
    public static IReadOnlyList<FileHandler> FileHandlers => Formats.FileHandlers;

    /// <summary>Whether a file name is a PDF (any letter case).</summary>
    /// <param name="name">A file name.</param>
    /// <returns><see langword="true"/> for <c>.pdf</c>.</returns>
    public static bool IsPdf(string name) =>
        string.Equals(CraftDocumentFormats.ExtensionOf(name), DocumentExtension, StringComparison.OrdinalIgnoreCase);
}
