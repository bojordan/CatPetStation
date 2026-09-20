using CatPetStation.Core;

namespace CatPetStation.Core.Tests;

public class PetEngineLedgeTests
{
    private static readonly PetDefinition Pet = ManifestReader.Parse("""
        { "name": "Testy", "img": "t.png", "width": 64, "height": 64,
          "animePos": {
            "stand": { "line": 0, "count": 2 },
            "walk":  { "line": 1, "count": 4 },
            "fall":  { "line": 2, "count": 3 },
            "drag":  { "line": 3, "count": 2 },
            "sleep": { "line": 4, "count": 2 }
          } }
        """);

    private static readonly ScreenBounds Screen = new(0, 0, 1920, 1080);

    /// <summary>A window top edge at y=600 spanning x 400..900.</summary>
    private static readonly Ledge WindowTop = new(400, 900, 600);

    private static PetEngine MakeEngine(int seed = 1) =>
        new(Pet, 64, 64, new Random(seed)) { X = 500, Y = 1080 - 64 };

    private static void Run(PetEngine engine, double seconds, params Ledge[] ledges)
    {
        var world = new PetWorld(Screen, ledges);
        const double dt = 1 / 60.0;
        for (var t = 0.0; t < seconds; t += dt)
            engine.Tick(dt, world);
    }

    private static void DropAt(PetEngine engine, double x, double y)
    {
        engine.BeginDrag();
        engine.X = x;
        engine.Y = y;
        engine.EndDrag(0, 0);
    }

    [Fact]
    public void DroppedPetLandsOnALedge()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);

        Run(engine, 3, WindowTop);

        Assert.Equal(600 - 64, engine.Y, precision: 3);
        Assert.Equal(WindowTop, engine.Support);
    }

    [Fact]
    public void PetWithoutEnoughFootingFallsPast()
    {
        var engine = MakeEngine();
        // Only ~14px of the 64px pet overlaps the ledge (< 40% footing).
        DropAt(engine, 350, 100);

        Run(engine, 3, WindowTop);

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.Null(engine.Support);
    }

    [Fact]
    public void ClosingTheWindowDropsThePet()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, WindowTop);
        Assert.Equal(WindowTop, engine.Support);

        Run(engine, 3); // ledge gone

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.Null(engine.Support);
    }

    [Fact]
    public void PetRidesAGentlyMovingWindow()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, WindowTop);

        // Window drifts up 10px per step — within the follow tolerance.
        var ledge = WindowTop;
        for (var i = 0; i < 5; i++)
        {
            ledge = ledge with { Y = ledge.Y - 10 };
            Run(engine, 0.1, ledge);
        }

        Assert.Equal(ledge.Y - 64, engine.Y, precision: 3);
        Assert.Equal(ledge, engine.Support);
    }

    [Fact]
    public void YankedDownWindowMakesThePetFallAndLandOnItAgain()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, WindowTop);

        // Window teleports far downward — beyond the follow tolerance, so the
        // pet falls… straight back onto the same window at its new position.
        var moved = WindowTop with { Y = 900 };
        Run(engine, 3, moved);

        Assert.Equal(900 - 64, engine.Y, precision: 3);
        Assert.Equal(moved, engine.Support);
    }

    [Fact]
    public void WindowYankedSidewaysDropsThePetToTheGround()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, WindowTop);

        var moved = WindowTop with { Left = 1200, Right = 1700 };
        Run(engine, 3, moved);

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.Null(engine.Support);
    }

    [Fact]
    public void WalkingOffTheEndOfALedgeFalls()
    {
        var engine = MakeEngine(seed: 5);
        DropAt(engine, 600, 100);

        // Long simulation: the pet idles, strolls, and sooner or later leaves the ledge.
        Run(engine, 240, WindowTop);

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.InRange(engine.X, Screen.Left, Screen.Right - 64);
    }

    [Fact]
    public void LandsOnTheHighestOfStackedLedges()
    {
        var high = new Ledge(400, 900, 400);
        var low = new Ledge(400, 900, 700);

        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, high, low);

        Assert.Equal(400 - 64, engine.Y, precision: 3);
        Assert.Equal(high, engine.Support);
    }

    [Fact]
    public void SleepingPetFallsWithItsWindowAndKeepsSleeping()
    {
        var engine = MakeEngine();
        DropAt(engine, 600, 100);
        Run(engine, 3, WindowTop);
        engine.SetSleeping(true);

        Run(engine, 5); // window closes under the sleeping pet

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.Equal(PetActivity.Sleep, engine.Activity);
    }

    [Fact]
    public void PetOnTheGroundIgnoresLedgesAbove()
    {
        var engine = MakeEngine(seed: 42);
        Run(engine, 60, WindowTop);

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        Assert.Null(engine.Support);
    }
}
