using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CatPetStation.Core;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CatPetStation.App;

/// <summary>
/// Binds one <see cref="PetEngine"/> to one <see cref="PetWindow"/>: forwards
/// ticks, moves the window, feeds mouse drags (with fling velocity) back into
/// the engine, and offers the per-pet right-click menu.
/// </summary>
public sealed class PetController : IDisposable
{
    private readonly PetEngine _engine;
    private readonly PetWindow _window;
    private readonly SpriteSheetImage _sprites;
    private readonly Action<PetController> _onRemove;

    private Point _grabOffset;
    private Point _lastDragPoint;
    private DateTime _lastDragTime;
    private Vector _dragVelocity;
    private bool _sleeping;

    public InstalledPack Pack { get; }

    public PetController(InstalledPack pack, double scale, Action<PetController> onRemove)
    {
        Pack = pack;
        _onRemove = onRemove;
        _sprites = new SpriteSheetImage(pack);

        var width = pack.Definition.FrameWidth * scale;
        var height = pack.Definition.FrameHeight * scale;
        _engine = new PetEngine(pack.Definition, width, height);

        var area = SystemParameters.WorkArea;
        _engine.X = area.Left + Random.Shared.NextDouble() * Math.Max(1, area.Width - width);
        _engine.Y = area.Top;   // new pets drop in from the top — a tiny bit of theater

        _window = new PetWindow { Width = width, Height = height, Title = pack.Definition.Name };
        _window.MouseLeftButtonDown += OnGrab;
        _window.MouseMove += OnDragMove;
        _window.MouseLeftButtonUp += OnRelease;
        _window.ContextMenu = BuildMenu();
        _window.Show();
        Render();
    }

    public void Tick(double dt)
    {
        var area = SystemParameters.WorkArea;
        _engine.Tick(dt, new ScreenBounds(area.Left, area.Top, area.Right, area.Bottom));
        Render();
    }

    public void SetSleeping(bool sleeping)
    {
        _sleeping = sleeping;
        _engine.SetSleeping(sleeping);
    }

    private void Render()
    {
        _window.Left = _engine.X;
        _window.Top = _engine.Y;
        var frame = _engine.CurrentFrame;
        _window.SetFrame(_sprites.GetFrame(frame), frame.FlipHorizontal);
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var nap = new MenuItem { Header = "Nap / wake" };
        nap.Click += (_, _) => SetSleeping(!_sleeping);
        menu.Items.Add(nap);

        var info = new MenuItem
        {
            Header = $"About {Pack.Definition.Name}",
            IsEnabled = false,
        };
        if (Pack.Definition.Author is { } author)
            info.Header = $"{Pack.Definition.Name} — {author}";
        menu.Items.Add(info);

        menu.Items.Add(new Separator());

        var remove = new MenuItem { Header = "Remove this pet" };
        remove.Click += (_, _) => _onRemove(this);
        menu.Items.Add(remove);

        return menu;
    }

    // ---- Dragging ---------------------------------------------------------

    private void OnGrab(object sender, MouseButtonEventArgs e)
    {
        _grabOffset = e.GetPosition(_window);
        _lastDragPoint = ToScreenDips(e);
        _lastDragTime = DateTime.UtcNow;
        _dragVelocity = default;
        _window.CaptureMouse();
        _engine.BeginDrag();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_window.IsMouseCaptured) return;

        var now = DateTime.UtcNow;
        var point = ToScreenDips(e);
        var dt = (now - _lastDragTime).TotalSeconds;
        if (dt > 0.001)
        {
            // Exponentially smoothed fling velocity so a jittery mouse still throws well.
            var instantaneous = (point - _lastDragPoint) / dt;
            _dragVelocity = _dragVelocity * 0.6 + instantaneous * 0.4;
        }
        _lastDragPoint = point;
        _lastDragTime = now;

        _engine.X = point.X - _grabOffset.X;
        _engine.Y = point.Y - _grabOffset.Y;
        Render();
    }

    private void OnRelease(object sender, MouseButtonEventArgs e)
    {
        if (!_window.IsMouseCaptured) return;
        _window.ReleaseMouseCapture();
        _engine.EndDrag(_dragVelocity.X, _dragVelocity.Y);
    }

    /// <summary>Cursor position in the same device-independent space as Window.Left/Top.</summary>
    private Point ToScreenDips(MouseEventArgs e)
    {
        var devicePoint = _window.PointToScreen(e.GetPosition(_window));
        var dpi = VisualTreeHelper.GetDpi(_window);
        return new Point(devicePoint.X / dpi.DpiScaleX, devicePoint.Y / dpi.DpiScaleY);
    }

    public void Dispose()
    {
        _window.Close();
    }
}
