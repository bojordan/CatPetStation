namespace CatPetStation.Core;

/// <summary>What the pet is currently doing.</summary>
public enum PetActivity
{
    Idle,
    Walk,
    Climb,   // moving up the left or right screen edge
    Crawl,   // hanging from the top edge, moving sideways
    Jump,
    Fall,
    Land,    // playing the landing tail of the fall animation
    Drag,    // held by the mouse
    Action,  // playing a random custom animation
    Sleep,   // paused by the user ("sleep" custom animation if the pack has one)
}

/// <summary>The screen work area the pet lives in, in device-independent pixels.</summary>
public readonly record struct ScreenBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}

/// <summary>The sprite the renderer should draw right now.</summary>
public readonly record struct SpriteFrame(string Animation, int FrameIndex, bool FlipHorizontal);

/// <summary>
/// The pet's brain and physics, with no UI dependencies: the WPF app (and the
/// future Swift port) calls <see cref="Tick"/> every frame and draws whatever
/// <see cref="CurrentFrame"/> says. Randomness is injected so tests are
/// deterministic.
///
/// A pet stands either on the ground (the bottom of the work area) or on a
/// <see cref="Ledge"/> — the top edge of someone else's window. Ledges are
/// re-validated every tick: if the window slides gently the pet rides along,
/// if it moves away or closes the pet falls to whatever is below.
/// </summary>
public sealed class PetEngine
{
    // Tuning constants, in device-independent pixels and seconds.
    public const double WalkSpeed = 65;
    public const double ClimbSpeed = 45;
    public const double CrawlSpeed = 45;
    public const double Gravity = 2200;
    public const double MaxFallSpeed = 1400;
    public const double JumpVelocity = -520;
    public const double ThrowDamping = 1.5;   // horizontal velocity decay per second while airborne
    public const double EdgeAttachDistance = 12;

    /// <summary>Minimum share of the pet's width that must rest on a ledge to stand on it.</summary>
    public const double MinLedgeFooting = 0.4;

    /// <summary>How far a ledge may shift vertically between ticks and still carry the pet.</summary>
    public const double LedgeFollowTolerance = 24;

    private readonly PetDefinition _pet;
    private readonly Random _random;
    private readonly string[] _customActions;

    private double _stateRemaining;   // seconds left in the current activity
    private double _animationTime;    // seconds since the current animation started
    private string _animation = KnownAnimations.Stand;
    private int _climbEdge;           // -1 = left edge, +1 = right edge
    private bool _resumeSleepAfterLanding;

    public PetEngine(PetDefinition pet, double width, double height, Random? random = null)
    {
        _pet = pet;
        Width = width;
        Height = height;
        _random = random ?? Random.Shared;
        _customActions = pet.CustomActionNames.ToArray();
        SetActivity(PetActivity.Idle);
    }

    /// <summary>Top-left position in device-independent pixels.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }

    /// <summary>+1 when facing right (the sprite's natural direction), -1 when facing left.</summary>
    public int Facing { get; private set; } = 1;

    public PetActivity Activity { get; private set; }

    /// <summary>The ledge currently under the pet's feet, or null when on the ground or airborne.</summary>
    public Ledge? Support { get; private set; }

    /// <summary>Raised when the pet lands after a fall; the app can play a sound or particle here.</summary>
    public event Action? Landed;

    public SpriteFrame CurrentFrame
    {
        get
        {
            var anim = _pet.Resolve(_animation);
            if (anim is null) return new SpriteFrame(KnownAnimations.Stand, 0, false);

            int frame;
            if (Activity is PetActivity.Fall or PetActivity.Jump && VelocityY >= 0)
            {
                // DPET convention: while airborne only the first fall frame shows.
                frame = 0;
            }
            else if (Activity is PetActivity.Land)
            {
                // The landing plays the remaining fall frames once.
                var landFrames = Math.Max(1, anim.FrameCount - 1);
                frame = 1 + Math.Min(landFrames - 1, (int)(_animationTime * anim.Fps));
                frame = Math.Min(frame, anim.FrameCount - 1);
            }
            else
            {
                var raw = (int)(_animationTime * anim.Fps);
                frame = anim.Loop ? raw % anim.FrameCount : Math.Min(raw, anim.FrameCount - 1);
            }

            // Climb art is edge-specific and never mirrored; crawl mirrors like ground movement.
            var flip = Facing < 0 && Activity is not PetActivity.Climb;
            return new SpriteFrame(_animation, frame, flip);
        }
    }

    /// <summary>Convenience overload for a world with no ledges.</summary>
    public void Tick(double dt, ScreenBounds bounds) => Tick(dt, new PetWorld(bounds));

    public void Tick(double dt, in PetWorld world)
    {
        _animationTime += dt;
        var bounds = world.Bounds;

        switch (Activity)
        {
            case PetActivity.Idle:
            case PetActivity.Sleep:
                if (!ValidateSupport(world)) break;
                if (Activity == PetActivity.Idle && CountDown(dt)) ChooseNextGroundActivity(bounds);
                break;

            case PetActivity.Action:
            case PetActivity.Land:
                if (!ValidateSupport(world)) break;
                if (CountDown(dt))
                {
                    var wasLanding = Activity == PetActivity.Land;
                    SetActivity(PetActivity.Idle);
                    if (wasLanding && _resumeSleepAfterLanding)
                    {
                        _resumeSleepAfterLanding = false;
                        SetSleeping(true);
                    }
                }
                break;

            case PetActivity.Walk:
                X += Facing * WalkSpeed * dt;
                if (X <= bounds.Left) HitWall(bounds.Left, -1);
                else if (X + Width >= bounds.Right) HitWall(bounds.Right - Width, +1);
                if (Activity != PetActivity.Walk) break;
                if (!ValidateSupport(world)) break; // strolled off the end of a ledge
                if (CountDown(dt)) SetActivity(PetActivity.Idle);
                break;

            case PetActivity.Climb:
                Y -= ClimbSpeed * dt;
                if (Y <= bounds.Top)
                {
                    Y = bounds.Top;
                    if (_pet.Has(KnownAnimations.Crawl)) StartCrawl();
                    else StartFall();
                }
                else if (CountDown(dt))
                {
                    StartFall(); // got tired and let go
                }
                break;

            case PetActivity.Crawl:
                X += Facing * CrawlSpeed * dt;
                var offEdge = X <= bounds.Left || X + Width >= bounds.Right;
                if (offEdge || CountDown(dt))
                {
                    X = Math.Clamp(X, bounds.Left, bounds.Right - Width);
                    StartFall();
                }
                break;

            case PetActivity.Jump:
            case PetActivity.Fall:
                var previousBottom = Y + Height;
                VelocityY = Math.Min(VelocityY + Gravity * dt, MaxFallSpeed);
                VelocityX -= VelocityX * ThrowDamping * dt;
                X = Math.Clamp(X + VelocityX * dt, bounds.Left, bounds.Right - Width);
                Y += VelocityY * dt;
                if (Activity == PetActivity.Jump && VelocityY >= 0)
                    SetAnimation(KnownAnimations.Fall);
                if (VelocityY >= 0)
                    TryLand(world, previousBottom);
                break;

            case PetActivity.Drag:
                break; // position is driven by the mouse
        }
    }

    /// <summary>The user picked the pet up.</summary>
    public void BeginDrag()
    {
        Activity = PetActivity.Drag;
        Support = null;
        SetAnimation(KnownAnimations.Drag);
    }

    /// <summary>The user let go, possibly with a fling velocity.</summary>
    public void EndDrag(double velocityX, double velocityY)
    {
        VelocityX = Math.Clamp(velocityX, -2000, 2000);
        VelocityY = Math.Clamp(velocityY, -2000, MaxFallSpeed);
        if (Math.Abs(velocityX) > 1) Facing = velocityX < 0 ? -1 : 1;
        Activity = PetActivity.Fall;
        SetAnimation(KnownAnimations.Fall);
    }

    /// <summary>Toggle the user-requested nap. Uses a "sleep" animation when the pack has one.</summary>
    public void SetSleeping(bool sleeping)
    {
        if (sleeping)
        {
            Activity = PetActivity.Sleep;
            SetAnimation(_pet.Has("sleep") ? "sleep" : KnownAnimations.Stand);
        }
        else if (Activity == PetActivity.Sleep)
        {
            SetActivity(PetActivity.Idle);
        }
        _resumeSleepAfterLanding = false;
    }

    // ---- Support & landing ------------------------------------------------

    /// <summary>
    /// While standing, checks that the surface underfoot still exists. A ledge
    /// that shifted a little carries the pet with it; one that moved away or
    /// disappeared drops the pet into a fall. Returns false when a fall began.
    /// </summary>
    private bool ValidateSupport(in PetWorld world)
    {
        var ground = world.Bounds.Bottom - Height;

        if (Support is null)
        {
            if (Y < ground - 1)
            {
                StartFallFromStanding();
                return false;
            }
            Y = Math.Min(Y, ground); // work area may have grown upward (taskbar moved)
            return true;
        }

        var feet = Y + Height;
        Ledge? best = null;
        var bestDistance = double.MaxValue;
        foreach (var ledge in world.Ledges)
        {
            if (ledge.OverlapWidth(X, X + Width) < Width * MinLedgeFooting) continue;
            var distance = Math.Abs(ledge.Y - feet);
            if (distance <= LedgeFollowTolerance && distance < bestDistance)
            {
                best = ledge;
                bestDistance = distance;
            }
        }

        if (best is { } carried)
        {
            Support = carried;
            Y = carried.Y - Height;
            return true;
        }

        StartFallFromStanding();
        return false;
    }

    /// <summary>
    /// While falling, lands on the highest surface whose top the pet's feet
    /// crossed this tick — a ledge with enough footing, or the ground.
    /// </summary>
    private void TryLand(in PetWorld world, double previousBottom)
    {
        var newBottom = Y + Height;
        Ledge? landedOn = null;
        var surfaceY = world.Bounds.Bottom;

        foreach (var ledge in world.Ledges)
        {
            if (ledge.Y > surfaceY || ledge.Y < previousBottom - 1 || ledge.Y > newBottom) continue;
            if (ledge.OverlapWidth(X, X + Width) < Width * MinLedgeFooting) continue;
            if (ledge.Y <= surfaceY)
            {
                surfaceY = ledge.Y;
                landedOn = ledge;
            }
        }

        if (landedOn is null && newBottom < world.Bounds.Bottom) return; // still airborne

        Support = landedOn;
        Y = surfaceY - Height;
        VelocityX = 0;
        VelocityY = 0;
        Landed?.Invoke();
        BeginLanding();
    }

    private void StartFallFromStanding()
    {
        _resumeSleepAfterLanding = Activity == PetActivity.Sleep;
        StartFall();
    }

    // ---- Activity selection -----------------------------------------------

    private void ChooseNextGroundActivity(ScreenBounds bounds)
    {
        var roll = _random.NextDouble();

        var nearLeft = X - bounds.Left < EdgeAttachDistance;
        var nearRight = bounds.Right - (X + Width) < EdgeAttachDistance;
        var canClimb = _pet.Has(KnownAnimations.Climb) && Support is null && (nearLeft || nearRight);

        if (canClimb && roll < 0.35)
        {
            StartClimb(nearLeft ? -1 : +1);
        }
        else if (roll < 0.45 && _customActions.Length > 0)
        {
            StartCustomAction();
        }
        else if (roll < 0.55 && _pet.Has(KnownAnimations.Jump))
        {
            StartJump();
        }
        else if (roll < 0.90)
        {
            StartWalk();
        }
        else
        {
            SetActivity(PetActivity.Idle); // keep loafing
        }
    }

    private void StartWalk()
    {
        Facing = _random.NextDouble() < 0.5 ? -1 : 1;
        Activity = PetActivity.Walk;
        _stateRemaining = 2 + _random.NextDouble() * 6;
        SetAnimation(KnownAnimations.Walk);
    }

    private void HitWall(double clampedX, int edge)
    {
        X = clampedX;
        if (_pet.Has(KnownAnimations.Climb) && Support is null && _random.NextDouble() < 0.5)
            StartClimb(edge);
        else
            Facing = -edge; // turn around and keep walking
    }

    private void StartClimb(int edge)
    {
        _climbEdge = edge;
        Facing = edge;
        Activity = PetActivity.Climb;
        Support = null;
        _stateRemaining = 3 + _random.NextDouble() * 8;
        SetAnimation(KnownAnimations.Climb);
    }

    private void StartCrawl()
    {
        Activity = PetActivity.Crawl;
        Facing = -_climbEdge; // crawl away from the edge we climbed
        _stateRemaining = 2 + _random.NextDouble() * 6;
        SetAnimation(KnownAnimations.Crawl);
    }

    private void StartJump()
    {
        VelocityY = JumpVelocity;
        VelocityX = Facing * 140;
        Activity = PetActivity.Jump;
        Support = null;
        SetAnimation(KnownAnimations.Jump);
    }

    private void StartCustomAction()
    {
        var name = _customActions[_random.Next(_customActions.Length)];
        var anim = _pet.Resolve(name)!;
        Activity = PetActivity.Action;
        _stateRemaining = Math.Max(0.5, anim.FrameCount / anim.Fps);
        SetAnimation(name);
    }

    private void StartFall()
    {
        Activity = PetActivity.Fall;
        Support = null;
        SetAnimation(KnownAnimations.Fall);
    }

    private void BeginLanding()
    {
        var fall = _pet.Resolve(KnownAnimations.Fall);
        var landingFrames = fall is null ? 0 : fall.FrameCount - 1;
        if (landingFrames <= 0)
        {
            SetActivity(PetActivity.Idle);
            if (_resumeSleepAfterLanding)
            {
                _resumeSleepAfterLanding = false;
                SetSleeping(true);
            }
            return;
        }
        Activity = PetActivity.Land;
        _animationTime = 0;
        _stateRemaining = landingFrames / (fall!.Fps <= 0 ? ManifestReader.DefaultDpetFps : fall.Fps);
    }

    private void SetActivity(PetActivity activity)
    {
        Activity = activity;
        _stateRemaining = activity == PetActivity.Idle ? 1.5 + _random.NextDouble() * 5 : 0;
        SetAnimation(KnownAnimations.Stand);
    }

    private void SetAnimation(string name)
    {
        if (_animation == name) return;
        _animation = name;
        _animationTime = 0;
    }

    private bool CountDown(double dt)
    {
        _stateRemaining -= dt;
        return _stateRemaining <= 0;
    }
}
