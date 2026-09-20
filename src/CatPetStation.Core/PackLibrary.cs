namespace CatPetStation.Core;

/// <summary>A pet pack installed on disk: a directory holding pet.json and its sprite sheet.</summary>
public sealed record InstalledPack(string Directory, PetDefinition Definition)
{
    public string SpriteSheetPath => Path.Combine(Directory, Definition.SpriteSheet);
}

/// <summary>
/// Finds installed packs. A pack is any direct subdirectory of a root that
/// contains a readable manifest (<c>pet.json</c>, or any single .json for
/// DPET-style packs). Broken packs are skipped, never fatal: one bad download
/// must not take the app down.
/// </summary>
public static class PackLibrary
{
    public static IReadOnlyList<InstalledPack> Enumerate(params string[] roots)
    {
        var packs = new List<InstalledPack>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var pack = TryLoad(dir);
                if (pack is not null) packs.Add(pack);
            }
        }
        return packs;
    }

    public static InstalledPack? TryLoad(string directory)
    {
        try
        {
            var manifestPath = Path.Combine(directory, "pet.json");
            if (!File.Exists(manifestPath))
                manifestPath = Directory.EnumerateFiles(directory, "*.json").FirstOrDefault() ?? "";
            if (!File.Exists(manifestPath)) return null;

            var definition = ManifestReader.Parse(File.ReadAllText(manifestPath));
            return File.Exists(Path.Combine(directory, definition.SpriteSheet))
                ? new InstalledPack(directory, definition)
                : null;
        }
        catch (Exception ex) when (ex is ManifestException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
