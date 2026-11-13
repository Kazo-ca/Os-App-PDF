using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kazo.Pdf.Tests;

/// <summary>The iframe page (decision 3) and the window's texts.</summary>
public sealed partial class HostPageTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string App = Path.Combine(Root, "src", "Kazo.Pdf");
    private static readonly string Page = File.ReadAllText(Path.Combine(App, "wwwroot", "host", "index.html"));

    [Fact]
    public void The_page_describes_PdfCraft_for_the_module_proxy()
    {
        // The <base> makes every relative address start in the build folder: pdfcraft.json there,
        // and the module's files, siblings of both folders.
        Assert.Contains("""<base href="../pdfcraft/">""", Page);
        Assert.Matches(
            """<canvas id="pdfcraft" data-host="page"\s+data-craft-app="pdfcraft" data-craft-build="pdfcraft.json">""",
            Page);
        Assert.Contains("""<script type="module" src="../../Kazo.Os.AppSdk.Craft/craft-proxy.js"></script>""", Page);
        Assert.Contains("""<link rel="stylesheet" href="../../Kazo.Os.AppSdk.Craft/craft-page.css">""", Page);
    }

    [Fact]
    public void The_base_comes_before_every_relative_address()
    {
        // A <base> only applies to the addresses after it; the page's comment is left out.
        string markup = Comment().Replace(Page, string.Empty);
        int baseAt = markup.IndexOf("<base ", StringComparison.Ordinal);
        Assert.True(baseAt >= 0);
        Assert.True(baseAt < markup.IndexOf("<link ", StringComparison.Ordinal));
        Assert.True(baseAt < markup.IndexOf("<script ", StringComparison.Ordinal));
        Assert.True(baseAt < markup.IndexOf("<meta name=\"pdfcraft-ocr-worker\"", StringComparison.Ordinal));
    }

    [Fact]
    public void The_build_and_the_OCR_worker_the_page_names_are_in_the_package()
    {
        string build = Path.Combine(App, "wwwroot", "pdfcraft");
        Assert.True(File.Exists(Path.Combine(build, "pdfcraft.json")));

        // PdfCraft starts its OCR workers from this tag's loader, relative to the page's base.
        Match worker = WorkerMeta().Match(Page);
        Assert.True(worker.Success, "the page has no <meta name=\"pdfcraft-ocr-worker\">");
        Assert.True(File.Exists(Path.Combine(build, worker.Groups["loader"].Value)), $"{worker.Groups["loader"].Value} is missing");
    }

    [Fact]
    public void The_page_has_no_inline_script_or_style()
    {
        Assert.DoesNotMatch("<script(?![^>]*\\ssrc=)[^>]*>", Page);
        Assert.DoesNotContain("<style", Page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", Page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void English_and_French_texts_have_the_same_keys()
    {
        Assert.Equal(KeysOf("Strings.resx"), KeysOf("Strings.fr.resx"));
    }

    [Fact]
    public void Every_text_the_window_reads_exists()
    {
        HashSet<string> keys = KeysOf("Strings.resx");
        IEnumerable<string> sources = Directory.EnumerateFiles(App, "PdfWindow*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
            .Select(File.ReadAllText);
        string[] used = [.. sources.SelectMany(text => TextKey().Matches(text).Select(match => match.Groups[1].Value)).Distinct()];

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, keys));
    }

    private static HashSet<string> KeysOf(string file) =>
        [.. XDocument.Load(Path.Combine(App, "Resources", file)).Root!.Elements("data").Select(data => (string)data.Attribute("name")!)];

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kazo.Pdf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kazo.Pdf.slnx not found above the test binaries.");
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex("""<meta name="pdfcraft-ocr-worker" content="(?<loader>[^"]+)">""")]
    private static partial Regex WorkerMeta();

    // L["Key"] and L["Key", ...] in the window, and the keys picked in a ternary (L[x ? "A" : "B"]).
    [GeneratedRegex("""(?:L\[|\? |: )"([A-Za-z]+)"(?=[\],])""")]
    private static partial Regex TextKey();
}
