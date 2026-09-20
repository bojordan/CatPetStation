using System.IO.Compression;
using CatPetStation.Core;

namespace CatPetStation.Core.Tests;

public class PackImporterTests : IDisposable
{
    private readonly string _workDir =
        Directory.CreateTempSubdirectory("catpetstation-tests-").FullName;

    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // magic
        0x00, 0x00, 0x00, 0x00, // fake remainder; importer checks magic, renderer validates fully
    ];

    private const string ValidManifest = """
        { "name": "Testy", "img": "testy.png", "width": 64, "height": 64,
          "animePos": { "stand": { "line": 0, "count": 2 } } }
        """;

    public void Dispose() => Directory.Delete(_workDir, recursive: true);

    private string MakeZip(params (string Name, byte[] Content)[] entries)
    {
        var path = Path.Combine(_workDir, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(content);
        }
        return path;
    }

    private string PacksRoot => Path.Combine(_workDir, "packs");

    private static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ImportsValidPack()
    {
        var zip = MakeZip(("testy.json", Utf8(ValidManifest)), ("testy.png", MinimalPng));

        var result = PackImporter.ImportZip(zip, PacksRoot);

        Assert.Equal(["Testy"], result.ImportedPets);
        var packs = PackLibrary.Enumerate(PacksRoot);
        Assert.Single(packs);
        Assert.Equal("Testy", packs[0].Definition.Name);
        Assert.True(File.Exists(packs[0].SpriteSheetPath));
    }

    [Fact]
    public void NeverExtractsExecutablesOrScripts()
    {
        var zip = MakeZip(
            ("totally-a-pet.exe", Utf8("MZ...")),
            ("run-me.bat", Utf8("@echo off")),
            ("helper.dll", Utf8("MZ...")),
            ("shortcut.lnk", Utf8("L")),
            ("script.ps1", Utf8("Invoke-Evil")),
            ("testy.json", Utf8(ValidManifest)),
            ("testy.png", MinimalPng));

        var result = PackImporter.ImportZip(zip, PacksRoot);

        Assert.Equal(["Testy"], result.ImportedPets);
        var extracted = Directory.EnumerateFiles(PacksRoot, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetExtension(f) ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Subset(new HashSet<string>([".json", ".png"], StringComparer.OrdinalIgnoreCase), extracted);
        Assert.Equal(5, result.Entries.Count(e => !e.Imported));
    }

    [Fact]
    public void ZipSlipEntriesCannotEscapeThePacksDirectory()
    {
        var evil = "..\\..\\evil.json";
        var zip = MakeZip(
            (evil, Utf8(ValidManifest)),
            ("..\\..\\testy.png", MinimalPng));

        PackImporter.ImportZip(zip, PacksRoot);

        // Whatever was written must live under the packs root — nothing above it.
        Assert.False(File.Exists(Path.Combine(_workDir, "evil.json")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_workDir)!, "evil.json")));
        foreach (var file in Directory.EnumerateFiles(PacksRoot, "*", SearchOption.AllDirectories))
            Assert.StartsWith(Path.GetFullPath(PacksRoot), Path.GetFullPath(file));
    }

    [Fact]
    public void RejectsPngThatIsNotReallyPng()
    {
        var zip = MakeZip(
            ("testy.json", Utf8(ValidManifest)),
            ("testy.png", Utf8("MZ this is an executable wearing a png hat")));

        var result = PackImporter.ImportZip(zip, PacksRoot);

        Assert.Empty(result.ImportedPets);
        Assert.Contains(result.Entries, e => !e.Imported && e.EntryName == "testy.png");
    }

    [Fact]
    public void RejectsAlternateDataStreamNames()
    {
        var zip = MakeZip(("innocent.png:evil.exe", Utf8("x")));
        var result = PackImporter.ImportZip(zip, PacksRoot);
        Assert.Empty(result.ImportedPets);
        Assert.All(result.Entries, e => Assert.False(e.Imported));
    }

    [Fact]
    public void ManifestWithoutItsSpriteSheetIsNotInstalled()
    {
        var zip = MakeZip(("testy.json", Utf8(ValidManifest)));
        var result = PackImporter.ImportZip(zip, PacksRoot);
        Assert.Empty(result.ImportedPets);
    }

    [Fact]
    public void DuplicateImportsGetDistinctDirectories()
    {
        var zip = MakeZip(("testy.json", Utf8(ValidManifest)), ("testy.png", MinimalPng));

        PackImporter.ImportZip(zip, PacksRoot);
        PackImporter.ImportZip(zip, PacksRoot);

        Assert.Equal(2, PackLibrary.Enumerate(PacksRoot).Count);
    }

    [Theory]
    [InlineData("..\\..\\boom.png", "boom.png")]
    [InlineData("a/b/c/sheet.png", "sheet.png")]
    [InlineData("....png", "unnamed.png")]
    [InlineData("we<>ird?.json", "we__ird_.json")]
    public void SanitizeFileNameFlattensHostileNames(string input, string expected)
    {
        Assert.Equal(expected, PackImporter.SanitizeFileName(input));
    }
}
