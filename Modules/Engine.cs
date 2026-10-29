using System.Windows.Threading;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>Owns the keyboard hook and routes keys to the modules, in order.</summary>
public sealed class Engine : IDisposable
{
    readonly Dispatcher _dispatcher;
    readonly DispatcherTimer _saveTimer;

    public AppSettings Settings { get; }
    public KeyboardHook Hook { get; } = new();
    public KeyboardMouseModule Mouse { get; }
    public HotkeysModule Hotkeys { get; }
    public LauncherModule Launcher { get; }
    public SnippetWheelModule Snippets { get; }
    public GamerModeModule Gamer { get; }
    public PanicModule Panic { get; }
    public KeySoundsModule Sounds { get; }
    public TypingTrainerModule Typing { get; }
    public MouseJigglerModule Jiggler { get; }
    public AppVolumeModule AppVolume { get; }
    public WindowPinModule WindowPin { get; }
    public TextToolsModule TextTools { get; }
    public TextExpanderModule Expander { get; }
    public WindowSnapperModule Snapper { get; }
    public GridJumpModule GridJump { get; }
    public SymbolLockModule SymbolLock { get; }
    public ScreenshotModule Screenshot { get; }
    public NotesModule Notes { get; }

    /// <summary>Set after restoring a backup, so quitting doesn't overwrite the restored settings.</summary>
    public bool SkipSaveOnExit { get; set; }

    public event Action? PinToggleRequested;
    public event Action? SafetyLockChanged;
    public event Action<string>? Error;
    public event Action<string>? Toast;

    public Engine(AppSettings settings)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        Settings = settings;
        Mouse = new KeyboardMouseModule(settings.Mouse);
        Hotkeys = new HotkeysModule(settings.Hotkeys, this);
        Launcher = new LauncherModule(settings.Launcher, this);
        Snippets = new SnippetWheelModule(settings.Snippets, this);
        Gamer = new GamerModeModule(settings.Gamer);
        Panic = new PanicModule(settings.Panic, this);
        Sounds = new KeySoundsModule(settings.Sounds, () => Gamer.Active);
        Typing = new TypingTrainerModule(settings.Typing, SaveSoon);
        Jiggler = new MouseJigglerModule(settings.Jiggler);
        AppVolume = new AppVolumeModule(settings.AppVolume, this);
        WindowPin = new WindowPinModule(settings.WindowPin, this);
        TextTools = new TextToolsModule(settings.TextTools, this);
        Expander = new TextExpanderModule(settings.Expander, Hook);
        Snapper = new WindowSnapperModule(settings.Snapper, this);
        GridJump = new GridJumpModule(settings.GridJump, this);
        SymbolLock = new SymbolLockModule(settings.SymbolLock, Hook);
        Screenshot = new ScreenshotModule(settings.Screenshot, this);
        Notes = new NotesModule(settings.Notes, this);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        Hook.KeyDown = OnKeyDown;
        Hook.KeyUp = vk =>
        {
            Snippets.OnKeyUp(vk);
            Mouse.OnKeyUp(vk);
            if (!SafetyLocked) Sounds.OnKeyUp(vk);
        };
        Hook.Install();
        Sounds.Apply();
        if (SafetyLocked) ApplySafetyLock();
    }

    public bool SafetyLocked => Settings.SafetyLock;

    /// <summary>
    /// Turns every module off (or back on). Turning it off again is only offered to a physical
    /// click in the window, so injected input can never undo it.
    /// </summary>
    public void SetSafetyLock(bool locked)
    {
        if (locked == Settings.SafetyLock) return;
        Settings.SafetyLock = locked;
        ApplySafetyLock();
        SaveSoon();
        SafetyLockChanged?.Invoke();
    }

    void ApplySafetyLock()
    {
        bool locked = Settings.SafetyLock;
        if (locked)
        {
            Mouse.SetActive(false);
            SymbolLock.SetActive(false);
            Jiggler.SetRunning(false);
        }
        Gamer.SetSuspended(locked);
    }

    bool OnKeyDown(int vk, Mods mods)
    {
        // The Safety Lock key comes before everything (even open pop-ups). It can only switch the lock on.
        if (Settings.SafetyKey.Matches(vk, mods))
        {
            if (SafetyLocked) ShowToast("Safety Lock is already on");
            else SetSafetyLock(true);
            return true;
        }
        if (SafetyLocked) return false;

        // Open pop-ups (wheel, grid, text tools menu) take keys first, then Gamer Mode's blocking,
        // then every module's hotkeys, then typing helpers, and the keyboard mouse last
        // (it owns plain keys like the arrows).
        bool blocked =
            Snippets.OnKeyDown(vk, mods)
            || GridJump.OnKeyDown(vk, mods)
            || TextTools.OnKeyDown(vk, mods)
            || Gamer.OnKeyDown(vk, mods)
            || Hotkeys.OnKeyDown(vk, mods)
            || Launcher.OnKeyDown(vk, mods)
            || Panic.OnKeyDown(vk, mods)
            || Jiggler.OnKeyDown(vk, mods)
            || AppVolume.OnKeyDown(vk, mods)
            || WindowPin.OnKeyDown(vk, mods)
            || Snapper.OnKeyDown(vk, mods)
            || Screenshot.OnKeyDown(vk, mods)
            || Notes.OnKeyDown(vk, mods)
            || SymbolLock.OnKeyDown(vk, mods)
            || (!Mouse.Active && Expander.OnKeyDown(vk, mods))
            || Mouse.OnKeyDown(vk, mods);

        // Listeners see every press but never block it.
        Sounds.OnKeyDown(vk);
        Typing.OnKeyDown(vk, mods, blocked);
        return blocked;
    }

    public void Post(Action action) => _dispatcher.BeginInvoke(action);

    public void RequestPinToggle() => PinToggleRequested?.Invoke();

    public void ReportError(string message) => Post(() => Error?.Invoke(message));

    /// <summary>Short on-screen message (call on the UI thread).</summary>
    public void ShowToast(string message) => Toast?.Invoke(message);

    public void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        if (SkipSaveOnExit) return;
        try { SettingsStore.Save(Settings); }
        catch (Exception ex) { Error?.Invoke($"Couldn't save settings: {ex.Message}"); }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        SaveNow();
        Hook.Dispose();
        Mouse.Dispose();
        Gamer.Dispose();
        Jiggler.Dispose();
        Sounds.Dispose();
    }
}
