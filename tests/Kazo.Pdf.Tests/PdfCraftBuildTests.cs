using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kazo.Pdf.Tests;

/// <summary>
/// The PdfCraft build in <c>src/Kazo.Pdf/wwwroot/pdfcraft/</c> matches its provenance file
/// (decision 5 of doc/en/plan.md): nothing was edited by hand, nothing is missing, nothing was added.
/// </summary>
public sealed partial class PdfCraftBuildTests
{
    private static readonly string BuildFolder = Path.Combine(FindRepositoryRoot(), "src", "Kazo.Pdf", "wwwroot", "pdfcraft");

    private static readonly JsonElement Provenance = ReadProvenance();

    [Fact]
    public void The_provenance_names_the_official_zip_or_the_commit_of_the_submodule()
    {
        JsonElement source = Provenance.GetProperty("source");
        string? kind = source.GetProperty("kind").GetString();

        if (kind == "release-zip")
        {
            // tools/vendor-pdfcraft.sh without --dir: the pinned official zip.
            Assert.Equal("0.21.1", Provenance.GetProperty("version").GetString());
            Assert.Equal("fe9708d46af191aebb56f70eb32cf8b112b0b9452d17ae6ba3a8c4a8665fe748", source.GetProperty("sha256").GetString());
            return;
        }

        // Work item 2: our own build. It must come from the commit the submodule points at, so
        // moving the submodule without rebuilding (or the reverse) fails here.
        Assert.Equal("source-build", kind);
        Assert.Equal(SubmoduleCommit(), source.GetProperty("commit").GetString());
        Assert.Equal("https://github.com/Kazo-ca/pdfcraft.git", source.GetProperty("repository").GetString());

        // PdfCraft's web release embeds craft-i18n at its pin: a build without it would show
        // English only, whatever language the OS speaks.
        Assert.Matches("^[0-9a-f]{40}$", source.GetProperty("craftI18n").GetString());
    }

    [Fact]
    public void The_version_shown_by_the_window_is_the_version_of_the_build()
    {
        Assert.Equal(PdfCraftBuild.Version, Provenance.GetProperty("version").GetString());
    }

    [Fact]
    public void The_wasm_stays_under_the_Kazo_OS_and_GitHub_file_size_limits()
    {
        // ADR 0245 of Kazo OS: 128 MiB per file; GitHub refuses a pushed file over 100 MB.
        // PdfCraft 0.21.1 is 60.6 MB.
        const long maximumFileBytes = 100L * 1000 * 1000;
        FileInfo wasm = new(Path.Combine(BuildFolder, Provenance.GetProperty("wasm").GetString()!));
        Assert.InRange(wasm.Length, 1, maximumFileBytes);
    }

    [Fact]
    public void The_build_fits_a_Kazo_OS_package()
    {
        // AppPackage of Kazo OS: 128 MiB unpacked and 2000 files for the whole package.
        long bytes = Provenance.GetProperty("files").EnumerateArray().Sum(file => file.GetProperty("size").GetInt64());
        Assert.InRange(bytes, 1, 120L * 1024 * 1024);
        Assert.InRange(Provenance.GetProperty("files").GetArrayLength(), 1, 1900);
    }

    [Fact]
    public void Every_listed_file_has_its_size_and_SHA_256()
    {
        foreach (JsonElement file in Provenance.GetProperty("files").EnumerateArray())
        {
            string path = file.GetProperty("path").GetString()!;
            string full = Path.Combine(BuildFolder, path);
            Assert.True(File.Exists(full), $"{path} is listed but missing");
            Assert.Equal(file.GetProperty("size").GetInt64(), new FileInfo(full).Length);
            using FileStream stream = File.OpenRead(full);
            Assert.Equal(file.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
    }

    [Fact]
    public void No_file_is_there_without_being_listed()
    {
        HashSet<string> listed = Provenance.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .Append("pdfcraft.json")
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<string> present = Directory.EnumerateFiles(BuildFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(BuildFolder, file).Replace('\\', '/'));

        Assert.All(present, path => Assert.True(listed.Contains(path), $"{path} is not in pdfcraft.json"));
    }

    [Fact]
    public void The_glue_and_the_wasm_are_listed_with_the_licences()
    {
        HashSet<string> listed = Provenance.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(Provenance.GetProperty("glue").GetString()!, listed);
        Assert.Contains(Provenance.GetProperty("wasm").GetString()!, listed);
        Assert.Contains("LICENSE-MIT", listed);
        Assert.Contains("LICENSE-APACHE", listed);
        Assert.Contains("NOTICE", listed);
    }

    [Fact]
    public void The_OCR_worker_and_models_are_listed_with_the_models_licences()
    {
        // Q2 = A: the models ship in the package, with their CC-BY-SA-4.0 licence texts.
        HashSet<string> listed = Provenance.GetProperty("files").EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        string loader = Provenance.GetProperty("worker").GetString()!;
        string worker = loader[..^"_loader.js".Length];
        Assert.Contains(loader, listed);
        Assert.Contains(worker + ".js", listed);
        Assert.Contains(worker + "_bg.wasm", listed);
        Assert.Contains("models/text-detection.onnx", listed);
        Assert.Contains("models/text-recognition.onnx", listed);
        Assert.Contains("models/text-detection.onnx.LICENCE.txt", listed);
        Assert.Contains("models/text-recognition.onnx.LICENCE.txt", listed);
        Assert.Contains("models/ATTRIBUTION.txt", listed);
    }

    [Fact]
    public void The_worker_loader_imports_the_worker_glue()
    {
        string loader = Provenance.GetProperty("worker").GetString()!;
        string text = File.ReadAllText(Path.Combine(BuildFolder, loader));
        string[] imports = [.. RelativeImport().Matches(text).Select(match => match.Groups["path"].Value)];
        Assert.Equal([loader[..^"_loader.js".Length] + ".js"], imports);
    }

    [Fact]
    public void Every_module_the_glue_imports_is_there()
    {
        string glue = File.ReadAllText(Path.Combine(BuildFolder, Provenance.GetProperty("glue").GetString()!));

        string[] imports = [.. RelativeImport().Matches(glue).Select(match => match.Groups["path"].Value)];

        Assert.NotEmpty(imports);
        Assert.All(imports, path => Assert.True(File.Exists(Path.Combine(BuildFolder, path)), $"{path} is imported but missing"));
    }

    // `import ... from './snippets/...'` in the wasm-bindgen glue.
    [GeneratedRegex(@"from\s+['""]\./(?<path>[^'""]+)['""]")]
    private static partial Regex RelativeImport();

    // The commit recorded for external/pdfcraft in git's index (the gitlink), which is there even
    // when the submodule itself is not checked out (CI fetches only the Kazo OS one).
    private static string SubmoduleCommit()
    {
        System.Diagnostics.ProcessStartInfo start = new("git", ["ls-files", "--stage", "--", "external/pdfcraft"])
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using System.Diagnostics.Process git = System.Diagnostics.Process.Start(start)!;
        string output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();

        // "160000 <commit> 0\texternal/pdfcraft"
        Match match = Gitlink().Match(output);
        Assert.True(git.ExitCode == 0 && match.Success, $"external/pdfcraft is not a submodule in git's index: {output}");
        return match.Groups["commit"].Value;
    }

    [GeneratedRegex(@"^160000 (?<commit>[0-9a-f]{40}) ")]
    private static partial Regex Gitlink();

    private static JsonElement ReadProvenance()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(BuildFolder, "pdfcraft.json")));
        return document.RootElement.Clone();
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
