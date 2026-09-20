using System.IO.Compression;

namespace CatPetStation.Core;

/// <summary>Outcome of importing one archive entry.</summary>
public sealed record ImportEntryResult(string EntryName, bool Imported, string Reason);

/// <summary>Outcome of importing one pack archive.</summary>
public sealed record ImportResult
{
    public required IReadOnlyList<ImportEntryResult> Entries { get; init; }
    public required IReadOnlyList<string> ImportedPets { get; init; }
    public bool AnyImported => ImportedPets.Count > 0;
}

/// <summary>
/// Imports pet packs from zip archives that may come from anywhere on the
/// internet — including places that also distribute malware. The importer is
/// built on one principle: <b>assets are data, never code</b>. It follows an
/// allowlist model:
///
/// <list type="bullet">
///   <item>Only <c>.json</c> and <c>.png</c> entries are ever extracted. Every
///     executable, script, shortcut, installer, or unknown file type in the
///     archive is skipped and reported. Nothing from the archive is ever run.</item>
///   <item>Entry names are re-derived: content is written to a sanitized flat
///     file name chosen by us, so hostile entry paths ("zip slip" attacks such
///     as <c>..\..\Startup\evil.exe</c>) cannot escape the destination folder.</item>
///   <item>PNG entries must actually begin with the PNG magic bytes, and both
///     per-file and whole-archive size limits guard against decompression bombs.</item>
///   <item>JSON entries must parse as a valid pet manifest before anything is
///     kept. A manifest that references a sprite sheet by path is rejected.</item>
///   <item>Windows alternate data streams and reserved device names are refused.</item>
/// </list>
///
/// The result is a per-entry report the UI shows to the user, so a rejected
/// file is a visible fact rather than a silent one.
/// </summary>
public static class PackImporter
{
    public const long MaxEntryBytes = 64 * 1024 * 1024;       // one sprite sheet
    public const long MaxTotalBytes = 512 * 1024 * 1024;      // whole archive, uncompressed
    public const int MaxEntries = 512;

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Imports every valid pet found in <paramref name="zipPath"/> into a new
    /// subdirectory of <paramref name="packsRoot"/>. Pets already installed with
    /// the same directory name are left untouched (a numbered suffix is used).
    /// </summary>
    public static ImportResult ImportZip(string zipPath, string packsRoot)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = new List<ImportEntryResult>();

        if (archive.Entries.Count > MaxEntries)
            throw new ManifestException(
                $"Archive contains {archive.Entries.Count} entries; the limit is {MaxEntries}.");

        // Pass 1: read manifests and PNG payloads into memory, validating as we go.
        var manifests = new List<(string EntryName, PetDefinition Definition, string Json)>();
        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue; // directory entry

            var verdict = ClassifyEntry(entry);
            if (verdict is not null)
            {
                entries.Add(new ImportEntryResult(entry.FullName, false, verdict));
                continue;
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxTotalBytes)
                throw new ManifestException("Archive exceeds the total uncompressed size limit.");

            var data = ReadEntryBounded(entry);
            if (data is null)
            {
                entries.Add(new ImportEntryResult(entry.FullName, false,
                    "Entry is larger than its declared size (possible decompression bomb)."));
                continue;
            }

            if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (!data.AsSpan().StartsWith(PngMagic))
                {
                    entries.Add(new ImportEntryResult(entry.FullName, false,
                        "File has a .png name but is not a PNG image."));
                    continue;
                }
                images[SanitizeFileName(entry.Name)] = data;
                entries.Add(new ImportEntryResult(entry.FullName, true, "PNG sprite sheet."));
            }
            else // .json — guaranteed by ClassifyEntry
            {
                string json;
                try
                {
                    json = System.Text.Encoding.UTF8.GetString(data);
                    var def = ManifestReader.Parse(json);
                    manifests.Add((entry.FullName, def, json));
                    entries.Add(new ImportEntryResult(entry.FullName, true, $"Pet manifest \"{def.Name}\"."));
                }
                catch (ManifestException ex)
                {
                    entries.Add(new ImportEntryResult(entry.FullName, false,
                        $"Not a valid pet manifest: {ex.Message}"));
                }
            }
        }

        // Pass 2: write out each manifest whose sprite sheet made it through validation.
        var importedPets = new List<string>();
        foreach (var (entryName, def, json) in manifests)
        {
            var sheetName = SanitizeFileName(def.SpriteSheet);
            if (!images.TryGetValue(sheetName, out var sheet))
            {
                entries.Add(new ImportEntryResult(entryName, false,
                    $"Skipped: sprite sheet \"{def.SpriteSheet}\" was not found or was rejected."));
                continue;
            }

            var dir = CreateUniqueDirectory(packsRoot, def.Name);
            File.WriteAllText(Path.Combine(dir, "pet.json"), json);
            File.WriteAllBytes(Path.Combine(dir, sheetName), sheet);
            importedPets.Add(def.Name);
        }

        return new ImportResult { Entries = entries, ImportedPets = importedPets };
    }

    /// <summary>Returns a rejection reason, or null when the entry may be extracted.</summary>
    internal static string? ClassifyEntry(ZipArchiveEntry entry)
    {
        var name = entry.Name;

        var ext = Path.GetExtension(name);
        if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return $"Only .png and .json files are imported; \"{ext}\" files are never extracted.";

        if (name.Contains(':'))
            return "Entry name contains ':' (alternate data streams are not allowed).";

        if (ReservedNames.Contains(Path.GetFileNameWithoutExtension(name)))
            return "Entry uses a reserved Windows device name.";

        if (entry.Length > MaxEntryBytes)
            return $"Entry is larger than the {MaxEntryBytes / (1024 * 1024)} MB per-file limit.";

        return null;
    }

    /// <summary>
    /// Reads at most the entry's declared length + 1 byte. A stream that keeps
    /// producing data beyond its declared length is lying (bomb) and is refused.
    /// </summary>
    private static byte[]? ReadEntryBounded(ZipArchiveEntry entry)
    {
        var declared = (int)entry.Length;
        using var stream = entry.Open();
        var buffer = new byte[declared];
        var read = 0;
        while (read < declared)
        {
            var n = stream.Read(buffer, read, declared - read);
            if (n == 0) break;
            read += n;
        }
        if (read < declared)
            return buffer[..read];
        return stream.ReadByte() == -1 ? buffer : null;
    }

    /// <summary>
    /// Reduces any entry name to a safe flat file name: the base name only
    /// (no directories), invalid characters replaced, never empty.
    /// </summary>
    internal static string SanitizeFileName(string entryName)
    {
        var baseName = entryName.Replace('\\', '/');
        baseName = baseName[(baseName.LastIndexOf('/') + 1)..];

        var invalid = Path.GetInvalidFileNameChars();
        var ext = Path.GetExtension(baseName);
        var stem = Path.GetFileNameWithoutExtension(baseName);
        stem = new string(stem.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().Trim('.');
        ext = new string(ext.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        return (string.IsNullOrEmpty(stem) ? "unnamed" : stem) + ext;
    }

    private static string CreateUniqueDirectory(string root, string petName)
    {
        var safe = SanitizeFileName(petName);
        if (string.IsNullOrWhiteSpace(safe) || ReservedNames.Contains(safe)) safe = "pet";

        var dir = Path.Combine(root, safe);
        for (var i = 2; Directory.Exists(dir); i++)
            dir = Path.Combine(root, $"{safe}-{i}");

        Directory.CreateDirectory(dir);
        return dir;
    }
}
