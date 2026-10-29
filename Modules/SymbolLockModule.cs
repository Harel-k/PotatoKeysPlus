using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Symbol Lock: Caps Lock for symbols. While it's on, the number row types ! @ # $ … and the
/// punctuation keys type their Shift symbol, without holding Shift. Holding Shift flips it back,
/// just like Caps Lock does for letters.
/// </summary>
public sealed class SymbolLockModule(SymbolLockSettings settings, KeyboardHook hook)
{
    public bool Active { get; private set; }
    public event Action? Changed;

    public void SetActive(bool on)
    {
        if (on && !settings.Enabled) on = false;
        if (on == Active) return;
        Active = on;
        Changed?.Invoke();
    }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled) return false;
        if (settings.Toggle.Matches(vk, mods))
        {
            SetActive(!Active);
            return true;
        }
        if (!Active || (mods & (Mods.Ctrl | Mods.Alt | Mods.Win)) != 0 || hook.IsDown(0xA5) || !IsSymbolKey(vk)) return false;

        bool shiftHeld = mods.HasFlag(Mods.Shift);
        if (!shiftHeld)
        {
            InputSender.Combo(new KeyBind(vk, Mods.Shift));
        }
        else
        {
            // Shift is held: type the plain key by briefly letting go of Shift for the app.
            var held = new[] { 0xA0, 0xA1 }.Where(hook.IsDown).ToArray();
            foreach (var s in held) InputSender.KeyUp(s);
            InputSender.KeyTap(vk);
            foreach (var s in held) InputSender.KeyDown(s);
        }
        return true;
    }

    bool IsSymbolKey(int vk) =>
        vk is >= 0x30 and <= 0x39 || (!settings.NumbersOnly && vk is (>= 0xBA and <= 0xC0) or (>= 0xDB and <= 0xDE));

    /// <summary>The symbol each number key gives in the current keyboard layout, for the settings page.</summary>
    public static string Preview()
    {
        var symbols = Enumerable.Range(0x31, 9).Append(0x30).Select(vk => Native.CharFor(vk, shift: true, altGr: false) ?? "?");
        return string.Join(" ", symbols);
    }
}
