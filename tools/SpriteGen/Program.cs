// SpriteGen — generates the built-in "Station Cat" sprite sheet and manifest.
//
// The art is defined as ASCII pixel grids right here in source, so the whole
// pet is reproducible, reviewable in a diff, and unambiguously MIT-licensed.
// Run from the repo root:  dotnet run --project tools/SpriteGen
//
// Grid conventions: each pose is a list of 32-char rows (short rows are padded).
// Poses are anchored to the bottom of the 32x32 cell, except "crawl", which
// hangs from the top of the cell (the pet clings to the top of the screen).
// Every pixel is scaled 4x, giving 128x128 frames — the DPET-typical size.

using System.IO;
using SpriteGen;

var repoRoot = FindRepoRoot();
var outDir = Path.Combine(repoRoot, "assets", "pets", "station-cat");
Directory.CreateDirectory(outDir);

var sheet = CatArt.BuildSheet();
SheetWriter.WritePng(sheet, Path.Combine(outDir, "cat.png"));
File.WriteAllText(Path.Combine(outDir, "pet.json"), CatArt.Manifest);
Console.WriteLine($"Wrote {Path.Combine(outDir, "cat.png")} and pet.json");

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "CatPetStation.slnx")))
            return d.FullName;
    throw new InvalidOperationException("Run SpriteGen from within the repository.");
}
