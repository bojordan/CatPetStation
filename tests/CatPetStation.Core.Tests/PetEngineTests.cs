using CatPetStation.Core;

namespace CatPetStation.Core.Tests;

public class PetEngineTests
{
    private static readonly PetDefinition Pet = ManifestReader.Parse("""
        { "name": "Testy", "img": "t.png", "width": 64, "height": 64,
          "animePos": {
            "stand": { "line": 0, "count": 2 },
            "walk":  { "line": 1, "count": 4 },
            "climb": { "line": 2, "count": 2 },
            "crawl": { "line": 3, "count": 2 },
            "fall":  { "line": 4, "count": 3 },
            "drag":  { "line": 5, "count": 2 }
          } }
        """);

    private static readonly ScreenBounds Screen = new(0, 0, 1920, 1080);

    private static PetEngine MakeEngine(int seed = 1) =>
        new(Pet, 64, 64, new Random(seed)) { X = 500, Y = 1080 - 64 };

    private static void Run(PetEngine engine, double seconds)
    {
        const double dt = 1 / 60.0;
        for (var t = 0.0; t < seconds; t += dt)
            engine.Tick(dt, Screen);
    }

    [Fact]
    public void DroppedPetFallsToTheGroundAndSettles()
    {
        var engine = MakeEngine();
        engine.BeginDrag();
        engine.X = 800;
        engine.Y = 200;
        engine.EndDrag(0, 0);

        Assert.Equal(PetActivity.Fall, engine.Activity);
        Run(engine, 5);

        Assert.Equal(1080 - 64, engine.Y, precision: 3);
        // Five seconds after landing the pet may already be loafing or strolling,
        // but it must be on the ground and done falling.
        Assert.True(engine.Activity is PetActivity.Idle or PetActivity.Walk or PetActivity.Action,
            $"Unexpected activity {engine.Activity}");
    }

    [Fact]
    public void AirborneFallShowsOnlyTheFirstFallFrame()
    {
        var engine = MakeEngine();
        engine.BeginDrag();
        engine.Y = 100;
        engine.EndDrag(0, 0);
        Run(engine, 0.2);

        var frame = engine.CurrentFrame;
        Assert.Equal(KnownAnimations.Fall, frame.Animation);
        Assert.Equal(0, frame.FrameIndex);
    }

    [Fact]
    public void PetStaysInsideScreenBoundsForALongTime()
    {
        var engine = MakeEngine(seed: 42);
        Run(engine, 300);

        Assert.InRange(engine.X, Screen.Left, Screen.Right - engine.Width);
        Assert.InRange(engine.Y, Screen.Top, Screen.Bottom - engine.Height);
    }

    [Fact]
    public void PetEventuallyWalks()
    {
        var engine = MakeEngine(seed: 7);
        var sawWalk = false;
        const double dt = 1 / 60.0;
        for (var t = 0.0; t < 120 && !sawWalk; t += dt)
        {
            engine.Tick(dt, Screen);
            sawWalk |= engine.Activity == PetActivity.Walk;
        }
        Assert.True(sawWalk, "Pet never walked in two simulated minutes.");
    }

    [Fact]
    public void ThrownPetKeepsHorizontalMomentum()
    {
        var engine = MakeEngine();
        engine.BeginDrag();
        engine.X = 500;
        engine.Y = 300;
        engine.EndDrag(600, 0);

        Run(engine, 0.5);
        Assert.True(engine.X > 520, $"Pet did not travel with the throw (X={engine.X}).");
        Assert.Equal(1, engine.Facing);
    }

    [Fact]
    public void SleepingPetStaysPut()
    {
        var engine = MakeEngine();
        engine.SetSleeping(true);
        var (x, y) = (engine.X, engine.Y);

        Run(engine, 60);

        Assert.Equal(PetActivity.Sleep, engine.Activity);
        Assert.Equal(x, engine.X);
        Assert.Equal(y, engine.Y);

        engine.SetSleeping(false);
        Assert.Equal(PetActivity.Idle, engine.Activity);
    }

    [Fact]
    public void DragAnimationPlaysWhileHeld()
    {
        var engine = MakeEngine();
        engine.BeginDrag();
        Assert.Equal(PetActivity.Drag, engine.Activity);
        Assert.Equal(KnownAnimations.Drag, engine.CurrentFrame.Animation);
    }

    [Fact]
    public void LandedEventFiresOnce()
    {
        var engine = MakeEngine();
        var landings = 0;
        engine.Landed += () => landings++;

        engine.BeginDrag();
        engine.Y = 400;
        engine.EndDrag(0, 0);
        Run(engine, 5);

        Assert.Equal(1, landings);
    }

    [Fact]
    public void PetWithOnlyAStandAnimationStillWorks()
    {
        var minimal = ManifestReader.Parse("""
            { "name": "Min", "img": "m.png", "width": 32, "height": 32,
              "animePos": { "stand": { "line": 0, "count": 1 } } }
            """);
        var engine = new PetEngine(minimal, 32, 32, new Random(3)) { X = 100, Y = 1080 - 32 };

        Run(engine, 120);

        Assert.InRange(engine.X, Screen.Left, Screen.Right - 32);
        Assert.Equal(KnownAnimations.Stand, engine.CurrentFrame.Animation);
    }
}
