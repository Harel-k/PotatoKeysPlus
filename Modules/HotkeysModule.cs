using System.Diagnostics;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>Hotkeys: user-made key combos that run an action anywhere in Windows.</summary>
public sealed class HotkeysModule(HotkeySettings settings, Engine engine)
{
    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled || KeyNames.IsModifier(vk)) return false;

        foreach (var h in settings.Items)
        {
            if (h.Enabled && h.Trigger.IsSet && h.Trigger.Vk == vk && h.Trigger.Mods == mods)
            {
                Run(h);
                return true;
            }
        }
        return false;
    }

    void Run(HotkeyEntry h)
    {
        switch (h.Action)
        {
            case HotkeyActionType.ToggleMouseMode:
                engine.Post(() => engine.Mouse.SetActive(!engine.Mouse.Active));
                return;
            case HotkeyActionType.TogglePin:
                engine.Post(engine.RequestPinToggle);
                return;
            case HotkeyActionType.Launch:
                var target = Environment.ExpandEnvironmentVariables(h.Target.Trim().Trim('"'));
                var args = h.Arguments;
                if (target.Length == 0) return;
                Task.Run(() =>
                {
                    try { Process.Start(new ProcessStartInfo(target, args) { UseShellExecute = true }); }
                    catch (Exception ex) { engine.ReportError($"Hotkey \"{h.Name}\" couldn't open \"{target}\": {ex.Message}"); }
                });
                return;
        }

        // Everything else injects input, which only works right once the trigger's
        // modifiers are let go (otherwise "hello" typed with Ctrl held becomes shortcuts).
        var action = h.Action;
        var text = h.Text;
        var keys = new KeyBind(h.Keys.Vk, h.Keys.Mods);
        var media = h.Media;
        var button = h.Button;
        Task.Run(() =>
        {
            WaitForModifiersReleased();
            switch (action)
            {
                case HotkeyActionType.TypeText: InputSender.TypeText(text); break;
                case HotkeyActionType.SendKeys: InputSender.Combo(keys); break;
                case HotkeyActionType.Media: InputSender.KeyTap(MediaVk(media)); break;
                case HotkeyActionType.MouseClick: InputSender.Click(button); break;
            }
        });
    }

    static int MediaVk(MediaAction m) => m switch
    {
        MediaAction.PlayPause => 0xB3,
        MediaAction.NextTrack => 0xB0,
        MediaAction.PreviousTrack => 0xB1,
        MediaAction.VolumeUp => 0xAF,
        MediaAction.VolumeDown => 0xAE,
        _ => 0xAD,
    };

    static void WaitForModifiersReleased()
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 1500)
        {
            bool anyDown = false;
            foreach (var vk in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
                if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0) anyDown = true;
            if (!anyDown) return;
            Thread.Sleep(10);
        }
    }
}
