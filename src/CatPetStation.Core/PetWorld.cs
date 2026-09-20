namespace CatPetStation.Core;

/// <summary>
/// A horizontal surface a pet can stand on — in practice, the visible part of
/// the top edge of someone else's window. Coordinates are device-independent
/// pixels; <see cref="Y"/> is where a standing pet's feet rest.
/// </summary>
public readonly record struct Ledge(double Left, double Right, double Y)
{
    public double Width => Right - Left;

    /// <summary>How much of the span [left, right] rests on this ledge.</summary>
    public double OverlapWidth(double left, double right) =>
        Math.Max(0, Math.Min(Right, right) - Math.Max(Left, left));
}

/// <summary>
/// Everything the engine knows about the space a pet lives in: the screen work
/// area plus the ledges currently available. The App layer rebuilds the ledge
/// list from the real window layout; the engine never talks to the OS.
/// </summary>
public readonly struct PetWorld(ScreenBounds bounds, IReadOnlyList<Ledge>? ledges = null)
{
    public ScreenBounds Bounds { get; } = bounds;
    public IReadOnlyList<Ledge> Ledges { get; } = ledges ?? [];
}
