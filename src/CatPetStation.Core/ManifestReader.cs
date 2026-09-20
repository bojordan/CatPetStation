using System.Text.Json;

namespace CatPetStation.Core;

/// <summary>
/// Thrown when a manifest is syntactically valid JSON but not a usable pet definition.
/// </summary>
public sealed class ManifestException(string message) : Exception(message);

/// <summary>
/// Parses pet manifests. Two dialects are accepted:
///
/// 1. The native CatPetStation format (<c>pet.json</c>):
///    <code>
///    { "format": "catpetstation/1", "name": "...", "spriteSheet": "cat.png",
///      "frameWidth": 128, "frameHeight": 128,
///      "animations": { "stand": { "row": 0, "frames": 4, "fps": 6 }, ... } }
///    </code>
///
/// 2. The DPET (Desktop Pet Engine) format, so existing community packs import
///    unchanged: <c>{ "name", "img", "width", "height", "animePos": { "stand":
///    { "line": 0, "count": 4 }, ... } }</c>. DPET rows are played at a fixed
///    default rate because the format does not carry timing.
///
/// Parsing is pure data-in, data-out: no file paths from the manifest are ever
/// resolved here, and sprite sheet names containing path separators are rejected.
/// </summary>
public static class ManifestReader
{
    public const double DefaultDpetFps = 8;
    public const int MaxFrameDimension = 1024;
    public const int MaxRows = 256;
    public const int MaxFramesPerRow = 256;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 8,
    };

    public static PetDefinition Parse(string jsonText)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ManifestException($"Manifest is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ManifestException("Manifest root must be a JSON object.");

            return root.TryGetProperty("animePos", out _)
                ? ParseDpet(root)
                : ParseNative(root);
        }
    }

    private static PetDefinition ParseNative(JsonElement root)
    {
        var name = RequireString(root, "name");
        var sheet = RequireSpriteSheetName(RequireString(root, "spriteSheet"));
        var width = RequireDimension(root, "frameWidth");
        var height = RequireDimension(root, "frameHeight");

        if (!root.TryGetProperty("animations", out var anims) || anims.ValueKind != JsonValueKind.Object)
            throw new ManifestException("Manifest must contain an \"animations\" object.");

        var table = new Dictionary<string, PetAnimation>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in anims.EnumerateObject())
        {
            var a = prop.Value;
            if (a.ValueKind != JsonValueKind.Object)
                throw new ManifestException($"Animation \"{prop.Name}\" must be an object.");

            table[prop.Name] = new PetAnimation
            {
                Row = RequireIntInRange(a, "row", 0, MaxRows - 1, prop.Name),
                FrameCount = RequireIntInRange(a, "frames", 1, MaxFramesPerRow, prop.Name),
                Fps = ReadFps(a),
                Loop = !a.TryGetProperty("loop", out var loop) || loop.ValueKind != JsonValueKind.False,
            };
        }

        return Build(name, sheet, width, height, table,
            author: OptionalString(root, "author"),
            license: OptionalString(root, "assetLicense"));
    }

    private static PetDefinition ParseDpet(JsonElement root)
    {
        var name = RequireString(root, "name");
        var sheet = RequireSpriteSheetName(RequireString(root, "img"));
        var width = RequireDimension(root, "width");
        var height = RequireDimension(root, "height");

        var anims = root.GetProperty("animePos");
        if (anims.ValueKind != JsonValueKind.Object)
            throw new ManifestException("\"animePos\" must be an object.");

        var table = new Dictionary<string, PetAnimation>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in anims.EnumerateObject())
        {
            var a = prop.Value;
            if (a.ValueKind != JsonValueKind.Object)
                throw new ManifestException($"Animation \"{prop.Name}\" must be an object.");

            table[prop.Name] = new PetAnimation
            {
                Row = RequireIntInRange(a, "line", 0, MaxRows - 1, prop.Name),
                FrameCount = RequireIntInRange(a, "count", 1, MaxFramesPerRow, prop.Name),
                Fps = DefaultDpetFps,
            };
        }

        return Build(name, sheet, width, height, table, author: null, license: null);
    }

    private static PetDefinition Build(
        string name, string sheet, int width, int height,
        Dictionary<string, PetAnimation> table, string? author, string? license)
    {
        if (table.Count == 0)
            throw new ManifestException("A pet needs at least one animation.");

        return new PetDefinition
        {
            Name = name,
            SpriteSheet = sheet,
            FrameWidth = width,
            FrameHeight = height,
            Animations = table,
            Author = author,
            AssetLicense = license,
        };
    }

    private static double ReadFps(JsonElement anim)
    {
        if (!anim.TryGetProperty("fps", out var fps)) return DefaultDpetFps;
        if (fps.ValueKind != JsonValueKind.Number || !fps.TryGetDouble(out var value) ||
            value is <= 0 or > 60)
            throw new ManifestException("\"fps\" must be a number between 0 and 60.");
        return value;
    }

    private static string RequireString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(el.GetString()))
            throw new ManifestException($"Manifest must contain a non-empty string \"{property}\".");
        return el.GetString()!.Trim();
    }

    private static string? OptionalString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int RequireDimension(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Number ||
            !el.TryGetInt32(out var value) || value < 1 || value > MaxFrameDimension)
            throw new ManifestException(
                $"\"{property}\" must be an integer between 1 and {MaxFrameDimension}.");
        return value;
    }

    private static int RequireIntInRange(JsonElement obj, string property, int min, int max, string context)
    {
        if (!obj.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Number ||
            !el.TryGetInt32(out var value) || value < min || value > max)
            throw new ManifestException(
                $"Animation \"{context}\": \"{property}\" must be an integer between {min} and {max}.");
        return value;
    }

    /// <summary>
    /// Sprite sheet references must be bare file names inside the pack — never
    /// paths. This is what makes a manifest unable to reach outside its pack.
    /// </summary>
    private static string RequireSpriteSheetName(string value)
    {
        if (value.Contains('/') || value.Contains('\\') || value.Contains("..") ||
            Path.IsPathRooted(value) || value.Contains(':'))
            throw new ManifestException(
                $"Sprite sheet reference \"{value}\" must be a bare file name inside the pack.");
        if (!value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new ManifestException("Sprite sheets must be PNG files.");
        return value;
    }
}
