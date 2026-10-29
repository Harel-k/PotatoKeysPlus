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

    public event Action? PinToggleRequested;
    public event Action<string>? Error;

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

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        Hook.KeyDown = OnKeyDown;
        Hook.KeyUp = vk =>
        {
            Snippets.OnKeyUp(vk);
            Mouse.OnKeyUp(vk);
            Sounds.OnKeyUp(vk);
        };
        Hook.Install();
        Sounds.Apply();
    }

    bool OnKeyDown(int vk, Mods mods)
    {
        // An open Snippet Wheel takes keys first, then Gamer Mode's blocking, then everyone's hotkeys,
        // and the keyboard mouse last (it owns plain keys like the arrows).
        bool blocked =
            Snippets.OnKeyDown(vk, mods)
            || Gamer.OnKeyDown(vk, mods)
            || Hotkeys.OnKeyDown(vk, mods)
            || Launcher.OnKeyDown(vk, mods)
            || Panic.OnKeyDown(vk, mods)
            || Jiggler.OnKeyDown(vk, mods)
            || Mouse.OnKeyDown(vk, mods);

        // Listeners see every press but never block it.
        Sounds.OnKeyDown(vk);
        Typing.OnKeyDown(vk, mods, blocked);
        return blocked;
    }

    public void Post(Action action) => _dispatcher.BeginInvoke(action);

    public void RequestPinToggle() => PinToggleRequested?.Invoke();

    public void ReportError(string message) => Post(() => Error?.Invoke(message));

    public void SaveSoon()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
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
