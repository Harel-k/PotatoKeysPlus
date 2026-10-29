using System.Diagnostics;
using NAudio.CoreAudioApi;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using Windows.Media.Control;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Panic Key: one combo hides every window, mutes the PC and pauses whatever is playing.
/// Press it again to put everything back the way it was.
/// </summary>
public sealed class PanicModule(PanicSettings settings, Engine engine)
{
    readonly List<string> _pausedApps = [];
    bool _weMuted;
    bool _weMinimized;
    bool _busy;

    public bool Panicked { get; private set; }
    public event Action? Changed;

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled || !settings.Hotkey.Matches(vk, mods)) return false;
        engine.Post(() => _ = TriggerAsync());
        return true;
    }

    public async Task TriggerAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (Panicked && settings.PressAgainToRestore) await RestoreAsync();
            else await PanicAsync();
        }
        finally
        {
            _busy = false;
            Changed?.Invoke();
        }
    }

    async Task PanicAsync()
    {
        _pausedApps.Clear();
        _weMuted = _weMinimized = false;

        if (settings.PauseMedia) await TryAsync(PauseMediaAsync);
        if (settings.Mute) Try(() =>
        {
            using var devices = new MMDeviceEnumerator();
            using var speaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (!speaker.AudioEndpointVolume.Mute)
            {
                speaker.AudioEndpointVolume.Mute = true;
                _weMuted = true;
            }
        });
        if (settings.MinimizeAll) Try(() => { Shell().MinimizeAll(); _weMinimized = true; });
        if (!string.IsNullOrWhiteSpace(settings.OpenTarget))
            Try(() => Process.Start(new ProcessStartInfo(Environment.ExpandEnvironmentVariables(settings.OpenTarget.Trim().Trim('"'))) { UseShellExecute = true }));

        if (settings.LockPc)
        {
            Native.LockWorkStation();
            Panicked = false; // after a lock there's nothing sensible to restore
            return;
        }
        Panicked = true;
    }

    async Task RestoreAsync()
    {
        if (_weMinimized) Try(() => { Shell().UndoMinimizeALL(); });
        if (_weMuted) Try(() =>
        {
            using var devices = new MMDeviceEnumerator();
            using var speaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            speaker.AudioEndpointVolume.Mute = false;
        });
        if (_pausedApps.Count > 0) await TryAsync(ResumeMediaAsync);
        Panicked = false;
    }

    async Task PauseMediaAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        foreach (var session in manager.GetSessions())
        {
            if (session.GetPlaybackInfo().PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) continue;
            if (await session.TryPauseAsync()) _pausedApps.Add(session.SourceAppUserModelId);
        }
    }

    async Task ResumeMediaAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        foreach (var session in manager.GetSessions())
            if (_pausedApps.Contains(session.SourceAppUserModelId))
                await session.TryPlayAsync();
        _pausedApps.Clear();
    }

    static dynamic Shell() => Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;

    static void Try(Action action)
    {
        try { action(); } catch { /* one failing step shouldn't stop the others */ }
    }

    static async Task TryAsync(Func<Task> action)
    {
        try { await action(); } catch { }
    }
}
