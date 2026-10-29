using System.Runtime.InteropServices;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Input;

/// <summary>
/// Global low-level keyboard hook. It is installed on the UI thread (which owns the
/// message loop), so every callback below runs on the UI thread and must stay fast.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    static readonly int[] ModifierVks = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

    readonly Native.LowLevelKeyboardProc _proc;
    readonly HashSet<int> _down = [];
    readonly HashSet<int> _swallowed = [];
    IntPtr _handle;
    bool _maskPending;
    Action<KeyBind?>? _capture;
    bool _captureCombo;

    /// <summary>Called on a fresh (non-repeat) key press. Return true to block the key from other apps.</summary>
    public Func<int, Mods, bool>? KeyDown { get; set; }

    /// <summary>Called on every physical key release.</summary>
    public Action<int>? KeyUp { get; set; }

    public KeyboardHook() => _proc = Proc;

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException($"Could not install the keyboard hook (error {Marshal.GetLastWin32Error()}).");
    }

    public Mods CurrentMods
    {
        get
        {
            // AltGr (Right Alt) arrives as Left Ctrl + Right Alt. On many layouts it types symbols
            // like @ € { }, so it must never look like Ctrl+Alt or it would trigger hotkeys.
            bool altGr = _down.Contains(0xA5);
            var mods = Mods.None;
            foreach (var vk in _down)
            {
                if (altGr && vk is 0xA5 or 0xA2) continue;
                mods |= KeyNames.ModFor(vk);
            }
            return mods;
        }
    }

    public bool IsDown(int vk) => _down.Contains(vk);

    /// <summary>
    /// The next key press is captured instead of being handled. With <paramref name="combo"/>
    /// modifiers are collected until a normal key is pressed. Esc cancels (callback gets null).
    /// </summary>
    public void BeginCapture(bool combo, Action<KeyBind?> done)
    {
        CancelCapture();
        _captureCombo = combo;
        _capture = done;
    }

    public void CancelCapture()
    {
        var previous = _capture;
        _capture = null;
        previous?.Invoke(null);
    }

    IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            // Skip only our own injected keys, so we never react to ourselves. Keys from other tools
            // (the Windows On-Screen Keyboard, accessibility apps, remappers) still work with Potato Keys+.
            bool ours = (data.flags & Native.LLKHF_INJECTED) != 0 && data.dwExtraInfo == InputSender.Signature;
            if (!ours)
            {
                int msg = (int)wParam;
                int vk = (int)data.vkCode;
                bool down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                bool block = down ? HandleDown(vk) : HandleUp(vk);
                if (block) return 1;
            }
        }
        return Native.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    bool HandleDown(int vk)
    {
        PruneStaleModifiers(vk);

        bool repeat = !_down.Add(vk);
        if (repeat) return _swallowed.Contains(vk);

        bool block;
        try
        {
            block = _capture != null ? HandleCapture(vk) : KeyDown?.Invoke(vk, CurrentMods) ?? false;
        }
        catch
        {
            block = false;
        }

        if (block)
        {
            // Whatever we block on the way down we also block on the way up, so apps never see half a key press.
            _swallowed.Add(vk);
            if (!KeyNames.IsModifier(vk) && (CurrentMods & (Mods.Alt | Mods.Win)) != 0) _maskPending = true;
        }
        return block;
    }

    bool HandleUp(int vk)
    {
        _down.Remove(vk);
        bool block = _swallowed.Remove(vk);
        try { KeyUp?.Invoke(vk); } catch { }

        if (_maskPending && (KeyNames.ModFor(vk) & (Mods.Alt | Mods.Win)) != 0)
        {
            InputSender.TapMaskKey();
            if ((CurrentMods & (Mods.Alt | Mods.Win)) == 0) _maskPending = false;
        }
        return block;
    }

    bool HandleCapture(int vk)
    {
        if (vk == 0x1B) // Esc
        {
            CancelCapture();
            return true;
        }
        if (_captureCombo && KeyNames.IsModifier(vk)) return false; // keep waiting for the main key

        var bind = new KeyBind(vk, _captureCombo ? CurrentMods : Mods.None);
        var done = _capture;
        _capture = null;
        done?.Invoke(bind);
        return true;
    }

    /// <summary>
    /// Key-ups can be missed (e.g. during Ctrl+Alt+Del or UAC prompts). Drop modifiers that
    /// Windows says are no longer held so hotkeys don't misfire.
    /// </summary>
    void PruneStaleModifiers(int current)
    {
        foreach (var vk in ModifierVks)
        {
            if (vk != current && _down.Contains(vk) && !_swallowed.Contains(vk)
                && (Native.GetAsyncKeyState(vk) & 0x8000) == 0)
                _down.Remove(vk);
        }
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }
}
