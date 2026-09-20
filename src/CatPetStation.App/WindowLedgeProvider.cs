using System.Runtime.InteropServices;
using System.Text;
using CatPetStation.Core;

namespace CatPetStation.App;

/// <summary>
/// Turns the tops of other applications' windows into <see cref="Ledge"/>s the
/// pets can sit on.
///
/// This is strictly read-only observation: the app enumerates top-level window
/// rectangles and nothing else — it never moves, resizes, activates, sends
/// input to, or reads the contents or titles of any window. Only geometry is
/// used, and none of it is stored or logged.
/// </summary>
public sealed class WindowLedgeProvider
{
    private const int MinLedgeWidthDips = 48;
    private const int MinWindowSizePx = 80;
    private const int MaxWindows = 48;

    /// <summary>Window classes that are shell furniture, not ledges (desktop, taskbar, …).</summary>
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
    };

    /// <summary>
    /// Computes the visible ledges, top of the z-order first, in DIPs.
    /// <paramref name="excluded"/> holds the pet windows themselves.
    /// </summary>
    public IReadOnlyList<Ledge> GetLedges(IReadOnlySet<nint> excluded, ScreenBounds workArea)
    {
        var dpiScale = GetDpiForSystem() / 96.0;
        var ledges = new List<Ledge>();
        var occluders = new List<Rect>(); // rects of windows higher in the z-order
        var seen = 0;

        EnumWindows((hwnd, _) =>
        {
            if (seen >= MaxWindows) return false;
            if (excluded.Contains(hwnd)) return true;
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
            if (IsCloaked(hwnd)) return true;

            var className = GetClassNameOf(hwnd);
            if (IgnoredClasses.Contains(className)) return true;

            if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out Rect rect, Marshal.SizeOf<Rect>()) != 0)
                return true;
            if (rect.Width < MinWindowSizePx || rect.Height < MinWindowSizePx) return true;

            seen++;

            var topDips = rect.Top / dpiScale;
            // A ledge needs headroom inside the work area to stand on; maximized
            // and full-height windows offer none.
            if (topDips > workArea.Top + 4 && topDips < workArea.Bottom - 4)
                AddVisibleSegments(ledges, rect, occluders, dpiScale, workArea);

            occluders.Add(rect);
            return true;
        }, 0);

        return ledges;
    }

    /// <summary>
    /// Emits the parts of this window's top edge not covered by windows above
    /// it in the z-order, as interval subtraction along the edge.
    /// </summary>
    private static void AddVisibleSegments(
        List<Ledge> ledges, Rect rect, List<Rect> occluders, double dpiScale, ScreenBounds workArea)
    {
        var segments = new List<(double Left, double Right)> { (rect.Left, rect.Right) };

        foreach (var above in occluders)
        {
            // The occluder hides this edge only where it overlaps the edge's row.
            if (above.Top >= rect.Top || above.Bottom <= rect.Top) continue;

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var (left, right) = segments[i];
                if (above.Right <= left || above.Left >= right) continue;

                segments.RemoveAt(i);
                if (above.Left > left) segments.Insert(i, (left, above.Left));
                if (above.Right < right) segments.Add((above.Right, right));
            }
        }

        foreach (var (left, right) in segments)
        {
            var ledge = new Ledge(
                Math.Max(left / dpiScale, workArea.Left),
                Math.Min(right / dpiScale, workArea.Right),
                rect.Top / dpiScale);
            if (ledge.Width >= MinLedgeWidthDips)
                ledges.Add(ledge);
        }
    }

    private static bool IsCloaked(nint hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static string GetClassNameOf(nint hwnd)
    {
        var buffer = new StringBuilder(64);
        _ = GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ---- Win32 ------------------------------------------------------------

    private const int DwmwaCloaked = 14;
    private const int DwmwaExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Rect
    {
        public readonly int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lparam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lparam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
