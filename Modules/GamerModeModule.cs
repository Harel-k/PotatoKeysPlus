using System.Runtime.InteropServices;
using System.Windows.Threading;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Gamer Mode: blocks keys that throw you out of a game (Win, Alt+Tab, Alt+F4) and the
/// Sticky/Filter/Toggle Keys popups. Turns on with a hotkey or when a game is in front.
/// </summary>
public sealed class GamerModeModule : IDisposable
{
    readonly GamerSettings _s;
    readonly DispatcherTimer _watch;
    bool _manual;
    string? _autoApp;
    string? _dismissedApp;
    bool _popupsChanged;
    Native.SKEYS _savedSticky, _savedToggle;
    Native.FILTERKEYS _savedFilter;

    public event Action? Changed;

    public GamerModeModule(GamerSettings settings)
    {
        _s = settings;
        _watch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _watch.Tick += (_, _) => WatchForeground();
        _watch.Start();
    }

    public bool Active => _s.Enabled && (_manual || _autoApp != null);

    /// <summary>The game that switched Gamer Mode on automatically, if any.</summary>
    public string? AutoApp => _autoApp;

    public void SetActive(bool on)
    {
        if (on == Active) return;
        if (on) _manual = true;
        else
        {
            _manual = false;
            // Turned off by hand while a game is open: don't switch straight back on for that game.
            if (_autoApp != null) { _dismissedApp = _autoApp; _autoApp = null; }
        }
        Apply();
    }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!_s.Enabled) return false;
        if (_s.Toggle.Matches(vk, mods))
        {
            SetActive(!Active);
            return true;
        }
        if (!Active) return false;

        if (_s.BlockWinKey && (vk is 0x5B or 0x5C || (vk == 0x1B && mods == Mods.Ctrl))) return true;
        if (_s.BlockAltTab && vk == 0x09 && mods.HasFlag(Mods.Alt)) return true;
        if (_s.BlockAltF4 && vk == 0x73 && mods.HasFlag(Mods.Alt)) return true;
        return false;
    }

    void WatchForeground()
    {
        string? game = null;
        if (_s.Enabled && _s.AutoForApps)
        {
            var name = Native.ForegroundProcessName();
            if (name != null && GameList().Contains(name, StringComparer.OrdinalIgnoreCase)) game = name;
        }
        if (game != _dismissedApp) _dismissedApp = null;
        if (game != null && game == _dismissedApp) game = null;

        if (game == _autoApp) return;
        _autoApp = game;
        Apply();
    }

    IEnumerable<string> GameList() =>
        _s.Apps.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? a : a + ".exe");

    /// <summary>Call after settings change so popup blocking follows the switches.</summary>
    public void Apply()
    {
        bool wantPopupsOff = Active && _s.BlockAccessibilityPopups;
        if (wantPopupsOff && !_popupsChanged) DisablePopups();
        else if (!wantPopupsOff && _popupsChanged) RestorePopups();
        Changed?.Invoke();
    }

    // The "press Shift 5 times" style shortcuts. We only switch off the shortcut (not the feature)
    // and never write to the user profile, so a crash can't leave them changed after a reboot.
    void DisablePopups()
    {
        _savedSticky = new Native.SKEYS { cbSize = (uint)Marshal.SizeOf<Native.SKEYS>() };
        _savedToggle = _savedSticky;
        _savedFilter = new Native.FILTERKEYS { cbSize = (uint)Marshal.SizeOf<Native.FILTERKEYS>() };
        Native.SystemParametersInfo(Native.SPI_GETSTICKYKEYS, _savedSticky.cbSize, ref _savedSticky, 0);
        Native.SystemParametersInfo(Native.SPI_GETTOGGLEKEYS, _savedToggle.cbSize, ref _savedToggle, 0);
        Native.SystemParametersInfo(Native.SPI_GETFILTERKEYS, _savedFilter.cbSize, ref _savedFilter, 0);

        if ((_savedSticky.dwFlags & Native.FEATURE_ON) == 0)
        {
            var v = _savedSticky; v.dwFlags &= ~Native.HOTKEY_ACTIVE;
            Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, v.cbSize, ref v, 0);
        }
        if ((_savedToggle.dwFlags & Native.FEATURE_ON) == 0)
        {
            var v = _savedToggle; v.dwFlags &= ~Native.HOTKEY_ACTIVE;
            Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, v.cbSize, ref v, 0);
        }
        if ((_savedFilter.dwFlags & Native.FEATURE_ON) == 0)
        {
            var v = _savedFilter; v.dwFlags &= ~Native.HOTKEY_ACTIVE;
            Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, v.cbSize, ref v, 0);
        }
        _popupsChanged = true;
    }

    void RestorePopups()
    {
        Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, _savedSticky.cbSize, ref _savedSticky, 0);
        Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, _savedToggle.cbSize, ref _savedToggle, 0);
        Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, _savedFilter.cbSize, ref _savedFilter, 0);
        _popupsChanged = false;
    }

    public void Dispose()
    {
        _watch.Stop();
        if (_popupsChanged) RestorePopups();
    }
}
