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

public sealed class AppVolumeSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Up { get; set; } = new(0xBB, Mods.Ctrl | Mods.Alt);     // Ctrl + Alt + =
    public KeyBind Down { get; set; } = new(0xBD, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + -
    public KeyBind Mute { get; set; } = new(0x30, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + 0
    public int StepPercent { get; set; } = 10;
}

public sealed class WindowPinSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Toggle { get; set; } = new(0x54, Mods.Ctrl | Mods.Alt);          // Ctrl + Alt + T
    public KeyBind MoreSeeThrough { get; set; } = new(0xDB, Mods.Ctrl | Mods.Alt);  // Ctrl + Alt + [
    public KeyBind LessSeeThrough { get; set; } = new(0xDD, Mods.Ctrl | Mods.Alt);  // Ctrl + Alt + ]
}

public sealed class TextToolsSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Menu { get; set; } = new(0x58, Mods.Ctrl | Mods.Alt);                     // Ctrl + Alt + X
    public KeyBind PlainPaste { get; set; } = new(0x56, Mods.Ctrl | Mods.Alt | Mods.Shift);   // Ctrl + Alt + Shift + V
}

public sealed class ExpanderSettings
{
    public bool Enabled { get; set; } = true;

    public List<ExpansionEntry> Items { get; set; } =
    [
        new() { Abbreviation = ";mail", Text = "you@example.com" },
        new() { Abbreviation = ";date", Text = "{date}" },
        new() { Abbreviation = ";time", Text = "{time}" },
        new() { Abbreviation = ";ty", Text = "Thank you so much!" },
        new() { Abbreviation = ";shrug", Text = @"¯\_(ツ)_/¯" },
    ];
}

public sealed class ExpansionEntry
{
    public string Abbreviation { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class SnapperSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Left { get; set; } = new(0x25, Mods.Ctrl | Mods.Alt);
    public KeyBind Right { get; set; } = new(0x27, Mods.Ctrl | Mods.Alt);
    public KeyBind Top { get; set; } = new(0x26, Mods.Ctrl | Mods.Alt);
    public KeyBind Bottom { get; set; } = new(0x28, Mods.Ctrl | Mods.Alt);
    public KeyBind TopLeft { get; set; } = new(0x24, Mods.Ctrl | Mods.Alt);      // Home
    public KeyBind TopRight { get; set; } = new(0x21, Mods.Ctrl | Mods.Alt);     // Page Up
    public KeyBind BottomLeft { get; set; } = new(0x23, Mods.Ctrl | Mods.Alt);   // End
    public KeyBind BottomRight { get; set; } = new(0x22, Mods.Ctrl | Mods.Alt);  // Page Down
    public KeyBind Maximize { get; set; } = new(0x0D, Mods.Ctrl | Mods.Alt);     // Enter
    public KeyBind Center { get; set; } = new(0x43, Mods.Ctrl | Mods.Alt);       // C
    public KeyBind NextMonitor { get; set; } = new(0x4D, Mods.Ctrl | Mods.Alt);  // M
    public KeyBind Restore { get; set; } = new(0x08, Mods.Ctrl | Mods.Alt);      // Backspace
    /// <summary>Pressing Left/Right again cycles 1/2 → 1/3 → 2/3 of the screen.</summary>
    public bool CycleSizes { get; set; } = true;
    public int GapPixels { get; set; }
}

public sealed class GridJumpSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Hotkey { get; set; } = new(0x4B, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + K (Ctrl + Alt + Space is taken by launchers like Claude)
    /// <summary>In mouse mode this single key opens the grid too.</summary>
    public KeyBind MouseModeKey { get; set; } = new(0x47);                   // G
    public double CellSize { get; set; } = 80;
    public bool ClickAfterJump { get; set; }
}

public sealed class SymbolLockSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Toggle { get; set; } = new(0x14, Mods.Shift);   // Shift + Caps Lock
    public bool NumbersOnly { get; set; }
    public bool ShowIndicator { get; set; } = true;
}

public sealed class ScreenshotSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Hotkey { get; set; } = new(0x2C, Mods.Ctrl);    // Ctrl + Print Screen
    public bool AlwaysSave { get; set; }
    public string SaveFolder { get; set; } = "";
}

public sealed class NotesSettings
{
    public bool Enabled { get; set; } = true;
    public KeyBind Hotkey { get; set; } = new(0x51, Mods.Ctrl | Mods.Alt);   // Ctrl + Alt + Q
    public bool OnTop { get; set; } = true;
    public double FontSize { get; set; } = 15;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 380;
    public int LastNote { get; set; }
}
