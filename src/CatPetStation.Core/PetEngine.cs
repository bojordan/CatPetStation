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

    private readonly PetDefinition _pet;
    private readonly Random _random;
    private readonly string[] _customActions;

    private double _stateRemaining;   // seconds left in the current activity
    private double _animationTime;    // seconds since the current animation started
    private string _animation = KnownAnimations.Stand;
    private int _climbEdge;           // -1 = left edge, +1 = right edge

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

    public void Tick(double dt, ScreenBounds bounds)
    {
        _animationTime += dt;
        var ground = bounds.Bottom - Height;

        switch (Activity)
        {
            case PetActivity.Idle:
            case PetActivity.Sleep:
                if (Y < ground - 1) { StartFall(); break; }
                if (Activity == PetActivity.Idle && CountDown(dt)) ChooseNextGroundActivity(bounds);
                break;

            case PetActivity.Action:
                if (CountDown(dt)) SetActivity(PetActivity.Idle);
                break;

            case PetActivity.Walk:
                X += Facing * WalkSpeed * dt;
                if (X <= bounds.Left) HitWall(bounds.Left, -1);
                else if (X + Width >= bounds.Right) HitWall(bounds.Right - Width, +1);
                if (Activity == PetActivity.Walk && CountDown(dt)) SetActivity(PetActivity.Idle);
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
                VelocityY = Math.Min(VelocityY + Gravity * dt, MaxFallSpeed);
                VelocityX -= VelocityX * ThrowDamping * dt;
                X = Math.Clamp(X + VelocityX * dt, bounds.Left, bounds.Right - Width);
                Y += VelocityY * dt;
                if (Activity == PetActivity.Jump && VelocityY >= 0)
                    SetAnimation(KnownAnimations.Fall);
                if (Y >= ground)
                {
                    Y = ground;
                    VelocityX = 0;
                    VelocityY = 0;
                    Landed?.Invoke();
                    BeginLanding();
                }
                break;

            case PetActivity.Land:
                if (CountDown(dt)) SetActivity(PetActivity.Idle);
                break;

            case PetActivity.Drag:
                break; // position is driven by the mouse
        }
    }

    /// <summary>The user picked the pet up.</summary>
    public void BeginDrag()
    {
        Activity = PetActivity.Drag;
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
    }

    private void ChooseNextGroundActivity(ScreenBounds bounds)
    {
        var roll = _random.NextDouble();

        var nearLeft = X - bounds.Left < EdgeAttachDistance;
        var nearRight = bounds.Right - (X + Width) < EdgeAttachDistance;
        var canClimb = _pet.Has(KnownAnimations.Climb) && (nearLeft || nearRight);

        if (canClimb && roll < 0.35)
        {
            StartClimb(nearLeft ? -1 : +1, bounds);
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
        if (_pet.Has(KnownAnimations.Climb) && _random.NextDouble() < 0.5)
            StartClimb(edge, default);
        else
            Facing = -edge; // turn around and keep walking
    }

    private void StartClimb(int edge, ScreenBounds bounds)
    {
        _climbEdge = edge;
        Facing = edge;
        Activity = PetActivity.Climb;
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
        SetAnimation(KnownAnimations.Fall);
    }

    private void BeginLanding()
    {
        var fall = _pet.Resolve(KnownAnimations.Fall);
        var landingFrames = fall is null ? 0 : fall.FrameCount - 1;
        if (landingFrames <= 0)
        {
            SetActivity(PetActivity.Idle);
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
