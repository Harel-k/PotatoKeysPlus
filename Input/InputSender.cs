using System.Runtime.InteropServices;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Input;

/// <summary>Injects keyboard and mouse input with SendInput.</summary>
public static class InputSender
{
    static readonly int InputSize = Marshal.SizeOf<Native.INPUT>();

    /// <summary>Stamped on everything we inject, so our own hook can recognise (and skip) it.</summary>
    public static readonly IntPtr Signature = new(0x504F5441); // "POTA"

    static void Send(params Native.INPUT[] inputs) => Native.SendInput((uint)inputs.Length, inputs, InputSize);

    static Native.INPUT Key(int vk, bool up)
    {
        uint flags = up ? Native.KEYEVENTF_KEYUP : 0;
        if (KeyNames.IsExtended(vk)) flags |= Native.KEYEVENTF_EXTENDEDKEY;
        return new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)Native.MapVirtualKey((uint)vk, 0), dwFlags = flags, dwExtraInfo = Signature } },
        };
    }

    static Native.INPUT Unicode(char c, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wScan = c, dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0), dwExtraInfo = Signature } },
    };

    static Native.INPUT Mouse(uint flags, int data = 0) => new()
    {
        type = Native.INPUT_MOUSE,
        U = new Native.InputUnion { mi = new Native.MOUSEINPUT { dwFlags = flags, mouseData = unchecked((uint)data), dwExtraInfo = Signature } },
    };

    public static void KeyTap(int vk) => Send(Key(vk, false), Key(vk, true));

    public static void KeyDown(int vk) => Send(Key(vk, false));

    public static void KeyUp(int vk) => Send(Key(vk, true));

    /// <summary>Presses a full combo, e.g. Ctrl + Shift + Esc.</summary>
    public static void Combo(KeyBind bind)
    {
        if (!bind.IsSet) return;
        var mods = new List<int>();
        if (bind.Mods.HasFlag(Mods.Ctrl)) mods.Add(0xA2);
        if (bind.Mods.HasFlag(Mods.Alt)) mods.Add(0xA4);
        if (bind.Mods.HasFlag(Mods.Shift)) mods.Add(0xA0);
        if (bind.Mods.HasFlag(Mods.Win)) mods.Add(0x5B);

        var inputs = new List<Native.INPUT>();
        foreach (var m in mods) inputs.Add(Key(m, false));
        inputs.Add(Key(bind.Vk, false));
        inputs.Add(Key(bind.Vk, true));
        for (int i = mods.Count - 1; i >= 0; i--) inputs.Add(Key(mods[i], true));
        Send([.. inputs]);
    }

    public static void TypeText(string text)
    {
        var inputs = new List<Native.INPUT>(text.Length * 2);
        foreach (var c in text)
        {
            if (c == '\r') continue;
            if (c == '\n') { inputs.Add(Key(0x0D, false)); inputs.Add(Key(0x0D, true)); continue; }
            inputs.Add(Unicode(c, false));
            inputs.Add(Unicode(c, true));
        }
        // Send in chunks so very long snippets don't overflow the input queue.
        for (int i = 0; i < inputs.Count; i += 200)
            Send([.. inputs.Skip(i).Take(200)]);
    }

    public static void MouseButton(ClickButton button, bool down) => Send(Mouse(button switch
    {
        ClickButton.Left => down ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP,
        ClickButton.Right => down ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP,
        _ => down ? Native.MOUSEEVENTF_MIDDLEDOWN : Native.MOUSEEVENTF_MIDDLEUP,
    }));

    public static void Click(ClickButton button)
    {
        MouseButton(button, true);
        MouseButton(button, false);
    }

    /// <summary>Relative mouse move (counts as real user activity for idle timers).</summary>
    public static void MouseMove(int dx, int dy) => Send(new Native.INPUT
    {
        type = Native.INPUT_MOUSE,
        U = new Native.InputUnion { mi = new Native.MOUSEINPUT { dx = dx, dy = dy, dwFlags = 0x0001, dwExtraInfo = Signature } },
    });

    public static void Wheel(int delta) => Send(Mouse(Native.MOUSEEVENTF_WHEEL, delta));

    public static void HWheel(int delta) => Send(Mouse(Native.MOUSEEVENTF_HWHEEL, delta));

    /// <summary>
    /// Taps an unassigned key so that releasing Alt/Win after a swallowed combo
    /// doesn't open the Start menu or an app's menu bar.
    /// </summary>
    public static void TapMaskKey() => KeyTap(0xE8);
}
