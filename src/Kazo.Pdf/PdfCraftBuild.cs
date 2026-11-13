namespace Kazo.Pdf;

/// <summary>
/// The PdfCraft web build this app carries in <c>wwwroot/pdfcraft/</c>, described by its
/// provenance file <c>pdfcraft.json</c> (written by <c>tools/vendor-pdfcraft.sh</c>, checked by
/// the tests).
/// </summary>
public static class PdfCraftBuild
{
    /// <summary>
    /// The PdfCraft version of the build. Kept equal to the <c>version</c> of
    /// <c>wwwroot/pdfcraft/pdfcraft.json</c> by a test.
    /// </summary>
    public const string Version = "0.21.1";

    /// <summary>Folder of the build under the app's static files (<c>_content/Kazo.Pdf/</c>).</summary>
    public const string Folder = "pdfcraft";
}
