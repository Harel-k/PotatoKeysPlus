using System.Text.Json.Serialization;
using PotatoKeysPlus.Input;

namespace PotatoKeysPlus.Models;

[Flags]
public enum Mods { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

/// <summary>A key (virtual-key code) plus optional modifiers. Vk 0 means "not set".</summary>
public sealed class KeyBind
{
    public int Vk { get; set; }
    public Mods Mods { get; set; }

    public KeyBind() { }
    public KeyBind(int vk, Mods mods = Mods.None) { Vk = vk; Mods = mods; }

    [JsonIgnore] public bool IsSet => Vk != 0;

    public override string ToString() => KeyNames.Describe(this);
}

public enum ClickButton { Left, Right, Middle }

public enum MediaAction { PlayPause, NextTrack, PreviousTrack, VolumeUp, VolumeDown, Mute }

public enum HotkeyActionType { TypeText, SendKeys, Launch, Media, MouseClick, ToggleMouseMode, TogglePin }

public sealed class AppSettings
{
    public bool Pinned { get; set; }
    public bool Compact { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 980;
    public double WindowHeight { get; set; } = 680;
    public string SelectedModule { get; set; } = "mouse";

    public bool AutoCheckUpdates { get; set; } = true;
    public DateTime? LastUpdateCheck { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool TrayHintShown { get; set; }

    public MouseSettings Mouse { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
}

public sealed class MouseSettings
{
    public bool Enabled { get; set; } = true;

    public KeyBind Toggle { get; set; } = new(0x77);       // F8
    public KeyBind Up { get; set; } = new(0x26);           // Up arrow
    public KeyBind Down { get; set; } = new(0x28);
    public KeyBind Left { get; set; } = new(0x25);
    public KeyBind Right { get; set; } = new(0x27);
    public KeyBind LeftClick { get; set; } = new(0x5A);    // Z
    public KeyBind RightClick { get; set; } = new(0x58);   // X
    public KeyBind MiddleClick { get; set; } = new(0x43);  // C
    public KeyBind ScrollUp { get; set; } = new(0x21);     // Page Up
    public KeyBind ScrollDown { get; set; } = new(0x22);   // Page Down
    public KeyBind ScrollLeft { get; set; } = new();
    public KeyBind ScrollRight { get; set; } = new();
    public KeyBind Boost { get; set; } = new(0xA0);        // Left Shift (hold)
    public KeyBind Precision { get; set; } = new(0xA2);    // Left Ctrl (hold)

    /// <summary>Pixels per second when a direction key is first pressed.</summary>
    public double StartSpeed { get; set; } = 250;
    /// <summary>Pixels per second after accelerating fully.</summary>
    public double MaxSpeed { get; set; } = 1600;
    /// <summary>Seconds to go from start speed to max speed.</summary>
    public double AccelTime { get; set; } = 0.8;
    public double BoostMultiplier { get; set; } = 2.5;
    public double PrecisionMultiplier { get; set; } = 0.25;
    /// <summary>Wheel notches per second while a scroll key is held.</summary>
    public double ScrollSpeed { get; set; } = 10;

    public bool ShowIndicator { get; set; } = true;
    public bool BlockKeys { get; set; } = true;
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;

    public List<HotkeyEntry> Items { get; set; } =
    [
        new() { Name = "Open Notepad", Trigger = new(0x4E, Mods.Ctrl | Mods.Alt), Action = HotkeyActionType.Launch, Target = "notepad.exe" },
        new() { Name = "Play / pause music", Trigger = new(0x50, Mods.Ctrl | Mods.Alt), Action = HotkeyActionType.Media, Media = MediaAction.PlayPause },
    ];
}

public sealed class HotkeyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New hotkey";
    public bool Enabled { get; set; } = true;
    public KeyBind Trigger { get; set; } = new();
    public HotkeyActionType Action { get; set; } = HotkeyActionType.TypeText;

    public string Text { get; set; } = "";
    public KeyBind Keys { get; set; } = new();
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public MediaAction Media { get; set; } = MediaAction.PlayPause;
    public ClickButton Button { get; set; } = ClickButton.Left;
}
