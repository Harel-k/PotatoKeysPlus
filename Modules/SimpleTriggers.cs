using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>Screenshot Tool: its hotkey opens the capture overlay (ScreenshotWindow does the rest).</summary>
public sealed class ScreenshotModule(ScreenshotSettings settings, Engine engine)
{
    public event Action? OpenRequested;
    public ScreenshotSettings Settings => settings;

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled || !settings.Hotkey.Matches(vk, mods)) return false;
        engine.Post(() => OpenRequested?.Invoke());
        return true;
    }
}

/// <summary>Quick Notes: its hotkey shows or hides the notes window.</summary>
public sealed class NotesModule(NotesSettings settings, Engine engine)
{
    public event Action? ToggleRequested;
    public NotesSettings Settings => settings;

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled || !settings.Hotkey.Matches(vk, mods)) return false;
        engine.Post(() => ToggleRequested?.Invoke());
        return true;
    }
}
