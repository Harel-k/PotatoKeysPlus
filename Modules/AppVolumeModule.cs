using System.Diagnostics;
using NAudio.CoreAudioApi;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// App Volume Keys: change the volume of just the app in front (turn the game down, keep Discord loud).
/// Matches audio sessions by program name, because browsers and games often play sound from a helper process.
/// </summary>
public sealed class AppVolumeModule(AppVolumeSettings settings, Engine engine)
{
    public sealed record AppSession(string App, int ProcessId, float Volume, bool Muted);

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled) return false;
        int change = settings.Up.Matches(vk, mods) ? 1 : settings.Down.Matches(vk, mods) ? -1 : 0;
        bool mute = settings.Mute.Matches(vk, mods);
        if (change == 0 && !mute) return false;

        var app = Native.ForegroundProcessName();
        Task.Run(() =>
        {
            string message;
            try { message = app == null ? "No app in front" : Adjust(app, change * settings.StepPercent / 100f, mute); }
            catch (Exception ex) { message = $"Couldn't change the volume: {ex.Message}"; }
            engine.Post(() => engine.ShowToast(message));
        });
        return true;
    }

    static string Adjust(string exe, float delta, bool toggleMute)
    {
        string name = StripExe(exe);
        using var devices = new MMDeviceEnumerator();
        using var speaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = speaker.AudioSessionManager.Sessions;
        float? result = null;
        bool? muted = null;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            if (!string.Equals(ProcessName((int)s.GetProcessID), name, StringComparison.OrdinalIgnoreCase)) continue;
            var v = s.SimpleAudioVolume;
            if (toggleMute)
            {
                muted ??= !v.Mute;
                v.Mute = muted.Value;
            }
            else
            {
                if (v.Mute) v.Mute = false;
                v.Volume = Math.Clamp((float)Math.Round((v.Volume + delta) * 20) / 20, 0f, 1f);
            }
            result = v.Volume;
        }
        if (result == null) return $"{name} isn't playing any sound right now";
        if (muted == true) return $"{name}: muted";
        return $"{name}: {result * 100:0}%";
    }

    /// <summary>Every app that is making (or can make) sound right now, for the settings page mixer.</summary>
    public static List<AppSession> Sessions()
    {
        var list = new Dictionary<string, AppSession>(StringComparer.OrdinalIgnoreCase);
        using var devices = new MMDeviceEnumerator();
        using var speaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = speaker.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            int pid = (int)s.GetProcessID;
            if (pid == 0) continue; // system sounds
            var name = ProcessName(pid);
            if (name == null || list.ContainsKey(name)) continue;
            list[name] = new AppSession(name, pid, s.SimpleAudioVolume.Volume, s.SimpleAudioVolume.Mute);
        }
        return list.Values.OrderBy(s => s.App).ToList();
    }

    public static void SetVolume(string app, float volume)
    {
        using var devices = new MMDeviceEnumerator();
        using var speaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = speaker.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
            if (string.Equals(ProcessName((int)sessions[i].GetProcessID), app, StringComparison.OrdinalIgnoreCase))
                sessions[i].SimpleAudioVolume.Volume = volume;
    }

    static string StripExe(string exe) => exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;

    static string? ProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
