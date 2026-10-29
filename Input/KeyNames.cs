using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Input;

public static class KeyNames
{
    static readonly Dictionary<int, string> Names = BuildNames();

    static Dictionary<int, string> BuildNames()
    {
        var n = new Dictionary<int, string>
        {
            [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x14] = "Caps Lock",
            [0x1B] = "Esc", [0x20] = "Space", [0x21] = "Page Up", [0x22] = "Page Down", [0x23] = "End",
            [0x24] = "Home", [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓",
            [0x2C] = "Print Screen", [0x2D] = "Insert", [0x2E] = "Delete",
            [0x5B] = "Left Win", [0x5C] = "Right Win", [0x5D] = "Menu",
            [0x6A] = "Num *", [0x6B] = "Num +", [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
            [0x90] = "Num Lock", [0x91] = "Scroll Lock",
            [0xA0] = "Left Shift", [0xA1] = "Right Shift", [0xA2] = "Left Ctrl", [0xA3] = "Right Ctrl",
            [0xA4] = "Left Alt", [0xA5] = "Right Alt",
            [0xAD] = "Mute", [0xAE] = "Volume Down", [0xAF] = "Volume Up",
            [0xB0] = "Next Track", [0xB1] = "Prev Track", [0xB2] = "Stop Media", [0xB3] = "Play/Pause",
        };
        for (int i = 0; i <= 9; i++) n[0x60 + i] = $"Num {i}";
        for (int i = 1; i <= 24; i++) n[0x6F + i] = $"F{i}";
        return n;
    }

    public static bool IsModifier(int vk) => ModFor(vk) != Mods.None;

    public static Mods ModFor(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => Mods.Shift,
        0x11 or 0xA2 or 0xA3 => Mods.Ctrl,
        0x12 or 0xA4 or 0xA5 => Mods.Alt,
        0x5B or 0x5C => Mods.Win,
        _ => Mods.None,
    };

    /// <summary>Keys that need KEYEVENTF_EXTENDEDKEY when injected.</summary>
    public static bool IsExtended(int vk) =>
        vk is (>= 0x21 and <= 0x28) or 0x2C or 0x2D or 0x2E or 0x5B or 0x5C or 0x5D or 0x6F or 0x90
            or 0xA3 or 0xA5 or (>= 0xAD and <= 0xB3);

    public static string KeyName(int vk)
    {
        if (Names.TryGetValue(vk, out var name)) return name;
        if (vk is (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A)) return ((char)vk).ToString();
        uint ch = Native.MapVirtualKey((uint)vk, 2) & 0x7FFF; // MAPVK_VK_TO_CHAR
        if (ch > 32) return ((char)ch).ToString();
        return $"Key 0x{vk:X2}";
    }

    public static string Describe(KeyBind bind)
    {
        if (!bind.IsSet) return "Not set";
        var parts = new List<string>(5);
        if (bind.Mods.HasFlag(Mods.Ctrl)) parts.Add("Ctrl");
        if (bind.Mods.HasFlag(Mods.Alt)) parts.Add("Alt");
        if (bind.Mods.HasFlag(Mods.Shift)) parts.Add("Shift");
        if (bind.Mods.HasFlag(Mods.Win)) parts.Add("Win");
        parts.Add(KeyName(bind.Vk));
        return string.Join(" + ", parts);
    }
}
