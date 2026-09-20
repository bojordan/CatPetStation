namespace CatPetStation.Core;

/// <summary>
/// A single animation: one row of the sprite sheet, played left to right.
/// </summary>
public sealed record PetAnimation
{
    /// <summary>Zero-based row index in the sprite sheet.</summary>
    public required int Row { get; init; }

    /// <summary>Number of frames in the row.</summary>
    public required int FrameCount { get; init; }

    /// <summary>Playback rate in frames per second.</summary>
    public double Fps { get; init; } = 8;

    /// <summary>Whether the animation repeats while its activity is active.</summary>
    public bool Loop { get; init; } = true;
}

/// <summary>
/// Well-known animation names. Packs may define any additional names;
/// unknown names are treated as random ground actions (matching DPET's convention).
/// </summary>
public static class KnownAnimations
{
    public const string Stand = "stand";
    public const string Walk = "walk";
    public const string Climb = "climb";   // moving up a screen edge
    public const string Crawl = "crawl";   // hanging from the top of the screen
    public const string Jump = "jump";
    public const string Fall = "fall";     // frame 0 = airborne, remaining frames = landing
    public const string Drag = "drag";     // held by the mouse

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Stand, Walk, Climb, Crawl, Jump, Fall, Drag,
    };
}

/// <summary>
/// Everything the runtime needs to know about one pet: where its sprite sheet
/// lives, the frame size, and the animation table. This is pure data — a pet
/// can never contain or trigger code.
/// </summary>
public sealed record PetDefinition
{
    public required string Name { get; init; }

    /// <summary>Sprite sheet file name, relative to the pack directory (no path separators allowed).</summary>
    public required string SpriteSheet { get; init; }

    /// <summary>Width of one frame in sprite-sheet pixels.</summary>
    public required int FrameWidth { get; init; }

    /// <summary>Height of one frame in sprite-sheet pixels.</summary>
    public required int FrameHeight { get; init; }

    /// <summary>Animation table, keyed by name (case-insensitive).</summary>
    public required IReadOnlyDictionary<string, PetAnimation> Animations { get; init; }

    /// <summary>Credit line shown in the UI; helps packs attribute their artists.</summary>
    public string? Author { get; init; }

    /// <summary>License of the art assets, e.g. "CC-BY-4.0". Shown in the UI.</summary>
    public string? AssetLicense { get; init; }

    /// <summary>Names of animations that are not well-known: candidates for random idle actions.</summary>
    public IEnumerable<string> CustomActionNames =>
        Animations.Keys.Where(k => !KnownAnimations.All.Contains(k));

    /// <summary>Returns the named animation, falling back to <c>stand</c>, then to any animation.</summary>
    public PetAnimation? Resolve(string name)
    {
        if (Animations.TryGetValue(name, out var anim)) return anim;
        if (Animations.TryGetValue(KnownAnimations.Stand, out var stand)) return stand;
        return Animations.Values.FirstOrDefault();
    }

    public bool Has(string name) => Animations.ContainsKey(name);
}
