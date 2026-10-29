using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Window Snapper: move the window in front to halves, thirds, corners, the centre, or the next
/// monitor with the keyboard. Pressing Left/Right again cycles ½ → ⅓ → ⅔.
/// </summary>
public sealed class WindowSnapperModule(SnapperSettings settings, Engine engine)
{
    enum Spot { Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight, Maximize, Center, NextMonitor, Restore }

    static readonly double[] Fractions = [0.5, 1 / 3.0, 2 / 3.0];
    readonly Dictionary<IntPtr, Native.RECT> _original = [];
    IntPtr _lastWindow;
    Spot _lastSpot;
    int _cycle;
    DateTime _lastTime;

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled) return false;
        Spot? spot =
            settings.Left.Matches(vk, mods) ? Spot.Left :
            settings.Right.Matches(vk, mods) ? Spot.Right :
            settings.Top.Matches(vk, mods) ? Spot.Top :
            settings.Bottom.Matches(vk, mods) ? Spot.Bottom :
            settings.TopLeft.Matches(vk, mods) ? Spot.TopLeft :
            settings.TopRight.Matches(vk, mods) ? Spot.TopRight :
            settings.BottomLeft.Matches(vk, mods) ? Spot.BottomLeft :
            settings.BottomRight.Matches(vk, mods) ? Spot.BottomRight :
            settings.Maximize.Matches(vk, mods) ? Spot.Maximize :
            settings.Center.Matches(vk, mods) ? Spot.Center :
            settings.NextMonitor.Matches(vk, mods) ? Spot.NextMonitor :
            settings.Restore.Matches(vk, mods) ? Spot.Restore : null;
        if (spot == null) return false;

        var hwnd = Native.GetForegroundWindow();
        engine.Post(() => Snap(hwnd, spot.Value));
        return true;
    }

    void Snap(IntPtr hwnd, Spot spot)
    {
        if (hwnd == IntPtr.Zero || Native.IsShellWindow(hwnd)) return;

        if (spot == Spot.Restore)
        {
            if (_original.Remove(hwnd, out var old)) Move(hwnd, old, exact: true);
            return;
        }
        if (spot == Spot.Maximize)
        {
            Remember(hwnd);
            Native.ShowWindow(hwnd, Native.IsZoomed(hwnd) ? 9 : 3); // restore / maximize
            return;
        }

        bool again = hwnd == _lastWindow && spot == _lastSpot && (DateTime.Now - _lastTime).TotalSeconds < 2;
        _cycle = again && settings.CycleSizes ? (_cycle + 1) % Fractions.Length : 0;
        _lastWindow = hwnd;
        _lastSpot = spot;
        _lastTime = DateTime.Now;

        Remember(hwnd);
        if (Native.IsZoomed(hwnd)) Native.ShowWindow(hwnd, 9);

        var monitor = Native.MonitorInfo(Native.MonitorFromWindow(hwnd, 2));
        var area = monitor.rcWork;

        if (spot == Spot.NextMonitor)
        {
            MoveToNextMonitor(hwnd, monitor);
            return;
        }

        int g = settings.GapPixels;
        double f = Fractions[_cycle];
        int x = area.Left, y = area.Top, w = area.Width, h = area.Height;
        int halfW = area.Width / 2, halfH = area.Height / 2;
        switch (spot)
        {
            case Spot.Left: w = (int)(area.Width * f); break;
            case Spot.Right: w = (int)(area.Width * f); x = area.Right - w; break;
            case Spot.Top: h = (int)(area.Height * f); break;
            case Spot.Bottom: h = (int)(area.Height * f); y = area.Bottom - h; break;
            case Spot.TopLeft: w = halfW; h = halfH; break;
            case Spot.TopRight: w = halfW; h = halfH; x = area.Right - w; break;
            case Spot.BottomLeft: w = halfW; h = halfH; y = area.Bottom - h; break;
            case Spot.BottomRight: w = halfW; h = halfH; x = area.Right - w; y = area.Bottom - h; break;
            case Spot.Center:
                double c = new[] { 0.6, 0.75, 0.45 }[_cycle];
                w = (int)(area.Width * c); h = (int)(area.Height * c);
                x = area.Left + (area.Width - w) / 2; y = area.Top + (area.Height - h) / 2;
                break;
        }
        Move(hwnd, new Native.RECT { Left = x + g, Top = y + g, Right = x + w - g, Bottom = y + h - g }, exact: false);
    }

    void MoveToNextMonitor(IntPtr hwnd, Native.MONITORINFO current)
    {
        var monitors = Native.AllMonitors();
        if (monitors.Count < 2) return;
        int index = monitors.FindIndex(m => m.rcMonitor.Left == current.rcMonitor.Left && m.rcMonitor.Top == current.rcMonitor.Top);
        var next = monitors[(index + 1) % monitors.Count].rcWork;
        var from = current.rcWork;
        Native.GetWindowRect(hwnd, out var r);
        // Keep the same relative position and size on the new screen.
        double sx = next.Width / (double)from.Width, sy = next.Height / (double)from.Height;
        int left = next.Left + (int)((r.Left - from.Left) * sx), top = next.Top + (int)((r.Top - from.Top) * sy);
        Move(hwnd, new Native.RECT { Left = left, Top = top, Right = left + (int)(r.Width * sx), Bottom = top + (int)(r.Height * sy) }, exact: true);
    }

    void Remember(IntPtr hwnd)
    {
        if (_original.ContainsKey(hwnd)) return;
        if (Native.GetWindowRect(hwnd, out var r)) _original[hwnd] = r;
        if (_original.Count > 50) _original.Remove(_original.Keys.First());
    }

    /// <summary>
    /// Windows 10/11 windows have invisible resize borders. Unless <paramref name="exact"/>, grow the
    /// rectangle so the visible edge lands exactly where we want it (no gaps between snapped windows).
    /// </summary>
    static void Move(IntPtr hwnd, Native.RECT target, bool exact)
    {
        if (!exact && Native.GetWindowRect(hwnd, out var outer)
            && Native.DwmGetWindowAttribute(hwnd, 9, out var visible, System.Runtime.InteropServices.Marshal.SizeOf<Native.RECT>()) == 0)
        {
            target.Left -= visible.Left - outer.Left;
            target.Top -= visible.Top - outer.Top;
            target.Right += outer.Right - visible.Right;
            target.Bottom += outer.Bottom - visible.Bottom;
        }
        Native.SetWindowPos(hwnd, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }
}
