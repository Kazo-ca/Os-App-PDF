using System.Text.Json;
using System.Xml.Linq;

namespace Kazo.Pdf.Tests;

/// <summary>
/// The licenses travel with the app (work item 4, THIRD-PARTY-NOTICES.md): the project links the
/// repository's license and notices into the package's web root, PdfCraft's own notices are in
/// its build folder, and the notices name the PdfCraft version actually bundled.
/// </summary>
public sealed class LegalFilesTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string PdfCraftFolder = Path.Combine(Root, "src", "Kazo.Pdf", "wwwroot", "pdfcraft");

    [Theory]
    [InlineData("LICENSE", "wwwroot/legal/LICENSE.txt")]
    [InlineData("THIRD-PARTY-NOTICES.md", "wwwroot/legal/THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md", "wwwroot/legal/THIRD-PARTY-NOTICES-fr.md")]
    public void The_project_puts_the_repository_legal_files_in_the_package(string file, string link)
    {
        XDocument project = XDocument.Load(Path.Combine(Root, "src", "Kazo.Pdf", "Kazo.Pdf.csproj"));

        // A Content item linked under wwwroot/ is a static web asset: KazoOsPack packs it.
        XElement? item = project.Descendants("Content")
            .FirstOrDefault(element => (string?)element.Attribute("Link") == link);
        Assert.NotNull(item);
        Assert.Equal($"../../{file}", (string?)item.Attribute("Include"));
        Assert.True(File.Exists(Path.Combine(Root, file)), $"{file} is missing at the repository root");
    }

    [Theory]
    [InlineData("LICENSE-MIT")]
    [InlineData("LICENSE-APACHE")]
    [InlineData("NOTICE")]
    [InlineData("models/ATTRIBUTION.txt")]
    [InlineData("models/text-detection.onnx.LICENCE.txt")]
    [InlineData("models/text-recognition.onnx.LICENCE.txt")]
    public void PdfCraft_ships_its_own_notices(string file)
    {
        Assert.True(File.Exists(Path.Combine(PdfCraftFolder, file)), $"pdfcraft/{file} is missing");
    }

    [Theory]
    [InlineData("ATTRIBUTION.md")]
    [InlineData("OFL-Inter.txt")]
    [InlineData("OFL-JetBrainsMono.txt")]
    [InlineData("OFL-DancingScript.txt")]
    [InlineData("LICENSE-lucide.txt")]
    [InlineData("egui-OFL.txt")]
    [InlineData("egui-UFL.txt")]
    [InlineData("egui-Hack-Regular.txt")]
    [InlineData("egui-emoji-icon-font-mit-license.txt")]
    [InlineData("ICU4X-LICENSE.txt")]
    [InlineData("CMap-LICENSE.txt")]
    [InlineData("prc-rs-LICENSE.txt")]
    [InlineData("LICENSE_FOXIT")]
    [InlineData("edit-session-LICENSE-MIT")]
    [InlineData("edit-session-LICENSE-APACHE")]
    public void The_license_files_PdfCraft_NOTICE_refers_to_ship_in_the_package(string file)
    {
        // PdfCraft's web release names these files in its NOTICE but does not carry them: they
        // are copied from the fork at the bundled commit, and listed in THIRD-PARTY-NOTICES.md.
        string path = Path.Combine(Root, "src", "Kazo.Pdf", "wwwroot", "legal", "pdfcraft", file);
        Assert.True(File.Exists(path), $"legal/pdfcraft/{file} is missing");
        Assert.True(new FileInfo(path).Length > 0, $"legal/pdfcraft/{file} is empty");
        Assert.Contains($"`{file}`", File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_bundled_PdfCraft_commit(string file)
    {
        // The license files above were copied at this commit: moving to another build without
        // copying them again fails here.
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(PdfCraftFolder, "pdfcraft.json")));
        string commit = provenance.RootElement.GetProperty("source").GetProperty("commit").GetString()!;
        Assert.Contains(commit, File.ReadAllText(Path.Combine(Root, file)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_bundled_PdfCraft_version(string file)
    {
        // Moving to another PdfCraft version without updating the notices fails here.
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(PdfCraftFolder, "pdfcraft.json")));
        string version = provenance.RootElement.GetProperty("version").GetString()!;

        string notices = File.ReadAllText(Path.Combine(Root, file));
        Assert.Contains($"| {version}, ", notices, StringComparison.Ordinal);
        Assert.Contains($"kazo/v{version}", notices, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("description.en.md", "PdfCraft")]
    [InlineData("description.fr.md", "PdfCraft")]
    [InlineData("description.en.md", "ArtCraft")]
    [InlineData("description.fr.md", "ArtCraft")]
    [InlineData("description.en.md", "CC-BY-SA-4.0")]
    [InlineData("description.fr.md", "CC-BY-SA-4.0")]
    public void The_store_listing_credits_PdfCraft_and_the_models(string file, string name)
    {
        Assert.Contains(name, File.ReadAllText(Path.Combine(Root, "store", file)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_store_tags_are_ones_the_catalog_accepts()
    {
        // Os-AppMarketPlace refuses any other tag (lower case, digits and '-', 30 characters at most):
        // Kazo Word 0.1.0 had "word processor" and its catalog folder had to be corrected by hand.
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "store", "store.json")));
        foreach (JsonElement tag in store.RootElement.GetProperty("tags").EnumerateArray())
        {
            Assert.Matches("^[a-z0-9][a-z0-9-]{0,29}$", tag.GetString());
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kazo.Pdf.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Kazo.Pdf.slnx not found above " + AppContext.BaseDirectory);
    }
}
