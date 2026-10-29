namespace PotatoKeysPlus.Models;

public sealed class LauncherSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Hotkey { get; set; } = new(0x20, Mods.Alt);   // Alt + Space
    public bool Calculator { get; set; } = true;
    public bool WebSearch { get; set; } = true;
    public string SearchUrl { get; set; } = "https://www.google.com/search?q={0}";
    /// <summary>How often each result was opened, so favourites rank first.</summary>
    public Dictionary<string, int> Usage { get; set; } = [];
}

public sealed class SnippetSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Trigger { get; set; } = new(0x53, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + S

    public List<HotkeyEntry> Items { get; set; } =
    [
        new() { Name = "Screenshot", Action = HotkeyActionType.SendKeys, Keys = new(0x53, Mods.Win | Mods.Shift) },
        new() { Name = "Copy", Action = HotkeyActionType.SendKeys, Keys = new(0x43, Mods.Ctrl) },
        new() { Name = "Paste", Action = HotkeyActionType.SendKeys, Keys = new(0x56, Mods.Ctrl) },
        new() { Name = "Undo", Action = HotkeyActionType.SendKeys, Keys = new(0x5A, Mods.Ctrl) },
        new() { Name = "Play / pause", Action = HotkeyActionType.Media, Media = MediaAction.PlayPause },
        new() { Name = "Emoji panel", Action = HotkeyActionType.SendKeys, Keys = new(0xBE, Mods.Win) },
        new() { Name = "Task Manager", Action = HotkeyActionType.SendKeys, Keys = new(0x1B, Mods.Ctrl | Mods.Shift) },
        new() { Name = "Calculator", Action = HotkeyActionType.Launch, Target = "calc.exe" },
    ];
}

public sealed class GamerSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Toggle { get; set; } = new(0x47, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + G
    public bool BlockWinKey { get; set; } = true;
    public bool BlockAltTab { get; set; } = true;
    public bool BlockAltF4 { get; set; }
    public bool BlockAccessibilityPopups { get; set; } = true;
    public bool AutoForApps { get; set; } = true;
    public string Apps { get; set; } = "javaw.exe, Minecraft.Windows.exe, RobloxPlayerBeta.exe, FortniteClient-Win64-Shipping.exe";
}

public sealed class PanicSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Hotkey { get; set; } = new(0x48, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + H
    public bool MinimizeAll { get; set; } = true;
    public bool Mute { get; set; } = true;
    public bool PauseMedia { get; set; } = true;
    public bool LockPc { get; set; }
    public string OpenTarget { get; set; } = "";
    public bool PressAgainToRestore { get; set; } = true;
}

public sealed class KeySoundSettings
{
    public bool Enabled { get; set; }
    public string Pack { get; set; } = "Thock";
    public double Volume { get; set; } = 0.5;
    public bool KeyUpSounds { get; set; }
    public bool MuteInGamerMode { get; set; } = true;
}

public sealed class TypingSettings
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, TypingDay> Days { get; set; } = [];
    public double BestBurstWpm { get; set; }
    public double BestTestWpm { get; set; }
    public List<TypingTestResult> Tests { get; set; } = [];
    /// <summary>Per letter: how often it was the expected key in a test, and how often it was missed.</summary>
    public Dictionary<string, int> LetterSeen { get; set; } = [];
    public Dictionary<string, int> LetterMissed { get; set; } = [];
}

public sealed class TypingDay
{
    public long Keys { get; set; }
    public long Chars { get; set; }
    public long Backspaces { get; set; }
    public double ActiveSeconds { get; set; }
    public double BestBurstWpm { get; set; }
}

public sealed class TypingTestResult
{
    public DateTime When { get; set; }
    public double Wpm { get; set; }
    public double Accuracy { get; set; }
}

public sealed class JigglerSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Toggle { get; set; } = new(0x4A, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + J
    public double IntervalSeconds { get; set; } = 60;
    public bool OnlyWhenIdle { get; set; } = true;
    public bool KeepScreenOn { get; set; } = true;
    public double StopAfterMinutes { get; set; }
}
