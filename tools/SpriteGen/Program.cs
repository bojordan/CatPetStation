// SpriteGen — generates the built-in cat sprite sheets and manifests.
//
// The art is defined as ASCII pixel grids right here in source, so every pet
// is reproducible, reviewable in a diff, and unambiguously MIT-licensed. All
// cats share the same poses; each Coat recolors them (palette swap plus an
// optional positional patch rule — see CatArt.Coats).
// Run from the repo root:  dotnet run --project tools/SpriteGen
//
// Grid conventions: each pose is a list of 32-char rows (short rows are padded).
// Poses are anchored to the bottom of the 32x32 cell, except "crawl", which
// hangs from the top of the cell (the pet clings to the top of the screen).
// Every pixel is scaled 4x, giving 128x128 frames — the DPET-typical size.

using System.IO;
using SpriteGen;

var repoRoot = FindRepoRoot();

foreach (var coat in CatArt.Coats)
{
    var outDir = Path.Combine(repoRoot, "assets", "pets", coat.Directory);
    Directory.CreateDirectory(outDir);
    SheetWriter.WritePng(CatArt.BuildSheet(coat), Path.Combine(outDir, "cat.png"));
    File.WriteAllText(Path.Combine(outDir, "pet.json"), CatArt.ManifestFor(coat));
    Console.WriteLine($"Wrote {coat.PetName,-12} -> {outDir}");
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "CatPetStation.slnx")))
            return d.FullName;
    throw new InvalidOperationException("Run SpriteGen from within the repository.");
}
