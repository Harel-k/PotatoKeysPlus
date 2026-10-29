using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>Pin Any Window: keep the window in front on top of everything, and make it see-through.</summary>
public sealed class WindowPinModule(WindowPinSettings settings, Engine engine)
{
    readonly List<IntPtr> _pinned = [];

    public event Action? Changed;

    /// <summary>Windows we pinned that still exist, with their titles.</summary>
    public List<(IntPtr Handle, string Title)> Pinned
    {
        get
        {
            _pinned.RemoveAll(h => !Native.IsWindow(h) || !IsTopmost(h));
            return _pinned.Select(h => (h, Native.WindowTitle(h))).ToList();
        }
    }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled) return false;
        var hwnd = Native.GetForegroundWindow();
        if (settings.Toggle.Matches(vk, mods))
        {
            engine.Post(() => TogglePin(hwnd));
            return true;
        }
        int step = settings.MoreSeeThrough.Matches(vk, mods) ? -1 : settings.LessSeeThrough.Matches(vk, mods) ? 1 : 0;
        if (step == 0) return false;
        engine.Post(() => ChangeOpacity(hwnd, step));
        return true;
    }

    public void TogglePin(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || Native.IsShellWindow(hwnd)) return;
        bool pin = !IsTopmost(hwnd);
        Native.SetWindowPos(hwnd, pin ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        if (pin) { if (!_pinned.Contains(hwnd)) _pinned.Add(hwnd); }
        else _pinned.Remove(hwnd);
        engine.ShowToast((pin ? "Pinned on top: " : "Unpinned: ") + Short(Native.WindowTitle(hwnd)));
        Changed?.Invoke();
    }

    public void Unpin(IntPtr hwnd)
    {
        if (IsTopmost(hwnd)) TogglePin(hwnd);
        SetOpacity(hwnd, 255);
    }

    void ChangeOpacity(IntPtr hwnd, int step)
    {
        if (hwnd == IntPtr.Zero || Native.IsShellWindow(hwnd)) return;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        byte alpha = 255;
        if ((ex & Native.WS_EX_LAYERED) != 0 && Native.GetLayeredWindowAttributes(hwnd, out _, out var a, out var flags) && (flags & Native.LWA_ALPHA) != 0)
            alpha = a;
        int percent = (int)Math.Round(alpha / 255.0 * 10) * 10 + step * 10;
        percent = Math.Clamp(percent, 20, 100); // never fully invisible
        SetOpacity(hwnd, (byte)(percent * 255 / 100));
        engine.ShowToast($"{Short(Native.WindowTitle(hwnd))}: {percent}% visible");
    }

    static void SetOpacity(IntPtr hwnd, byte alpha)
    {
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        if (alpha >= 255)
        {
            if ((ex & Native.WS_EX_LAYERED) != 0) Native.SetLayeredWindowAttributes(hwnd, 0, 255, Native.LWA_ALPHA);
            return;
        }
        if ((ex & Native.WS_EX_LAYERED) == 0) Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_LAYERED);
        Native.SetLayeredWindowAttributes(hwnd, 0, alpha, Native.LWA_ALPHA);
    }

    static bool IsTopmost(IntPtr hwnd) => (Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0;

    static string Short(string title) => title.Length > 40 ? title[..40] + "…" : title.Length == 0 ? "window" : title;
}
