using PotatoKeysPlus.Input;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Snippet Wheel: hold the trigger and a round menu of up to 8 actions pops up at the mouse.
/// Pick with 1-8, the arrows or the mouse, then let go of the trigger (or press Enter).
/// A quick tap keeps the wheel open until you pick or press Esc.
/// </summary>
public sealed class SnippetWheelModule(SnippetSettings settings, Engine engine)
{
    public const int MaxItems = 8;

    public interface IView
    {
        void Open(IReadOnlyList<HotkeyEntry> items);
        void Select(int index);
        void Close();
    }

    DateTime _openedAt;
    bool _triggerHeld;

    public IView? View { get; set; }
    public bool IsOpen { get; private set; }
    public int Selected { get; private set; } = -1;
    public SnippetSettings Settings => settings;

    IReadOnlyList<HotkeyEntry> Items => settings.Items.Take(MaxItems).ToList();

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (IsOpen)
        {
            int count = Items.Count;
            int digit = vk is >= 0x31 and <= 0x38 ? vk - 0x31 : vk is >= 0x61 and <= 0x68 ? vk - 0x61 : -1;
            if (digit >= 0)
            {
                if (digit < count) Fire(digit);
                return true;
            }
            switch (vk)
            {
                case 0x27 or 0x28: Select(Selected < 0 ? 0 : (Selected + 1) % count); return true;          // → ↓
                case 0x25 or 0x26: Select(Selected < 0 ? count - 1 : (Selected - 1 + count) % count); return true; // ← ↑
                case 0x0D: if (Selected >= 0) Fire(Selected); else Close(); return true;
                case 0x1B: Close(); return true;
            }
            if (KeyNames.IsModifier(vk)) return false;
            Close(); // any other key: get out of the way and let it through
            return false;
        }

        if (!settings.Enabled || settings.Items.Count == 0 || !settings.Trigger.Matches(vk, mods)) return false;
        IsOpen = true;
        Selected = -1;
        _openedAt = DateTime.Now;
        _triggerHeld = true;
        View?.Open(Items);
        return true;
    }

    public void OnKeyUp(int vk)
    {
        if (!IsOpen || !_triggerHeld || vk != settings.Trigger.Vk) return;
        _triggerHeld = false;
        if ((DateTime.Now - _openedAt).TotalMilliseconds < 350) return; // quick tap: stay open
        if (Selected >= 0) Fire(Selected);
        else Close();
    }

    /// <summary>Opens the wheel without the trigger held (the settings page's preview button).</summary>
    public void OpenSticky()
    {
        if (IsOpen || settings.Items.Count == 0) return;
        IsOpen = true;
        Selected = -1;
        _triggerHeld = false;
        View?.Open(Items);
    }

    public void Select(int index)
    {
        Selected = index;
        View?.Select(index);
    }

    public void Fire(int index)
    {
        var items = Items;
        if (index < 0 || index >= items.Count) return;
        Close();
        engine.Hotkeys.Execute(items[index]);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Selected = -1;
        View?.Close();
    }
}
