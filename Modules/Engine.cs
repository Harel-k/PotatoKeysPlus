using System.Windows;
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

    public event Action? PinToggleRequested;
    public event Action<string>? Error;

    public Engine(AppSettings settings)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        Settings = settings;
        Mouse = new KeyboardMouseModule(settings.Mouse);
        Hotkeys = new HotkeysModule(settings.Hotkeys, this);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        // Hotkeys get first pick, then the keyboard mouse.
        Hook.KeyDown = (vk, mods) => Hotkeys.OnKeyDown(vk, mods) || Mouse.OnKeyDown(vk, mods);
        Hook.KeyUp = Mouse.OnKeyUp;
        Hook.Install();
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
    }
}
