using PotatoKeysPlus.Audio;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>Key Sounds: plays a mechanical-keyboard style sound for every key press.</summary>
public sealed class KeySoundsModule(KeySoundSettings settings, Func<bool> gamerModeActive) : IDisposable
{
    readonly SoundEngine _engine = new();
    readonly Random _random = new();
    SoundSynth.Pack? _pack;
    string? _packName;

    public string? LastError { get; private set; }

    /// <summary>Opens or closes the audio device to match the settings.</summary>
    public void Apply()
    {
        try
        {
            if (settings.Enabled) _engine.Start();
            else _engine.Stop();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't open the speakers: {ex.Message}";
        }
        if (_engine.IsRunning && (_pack == null || _packName != settings.Pack))
        {
            _pack = SoundSynth.Build(settings.Pack, _engine.SampleRate);
            _packName = settings.Pack;
        }
    }

    public void OnKeyDown(int vk)
    {
        if (!ShouldPlay()) return;
        var pack = _pack!;
        float[] sound = vk switch
        {
            0x0D when pack.Enter != null => pack.Enter,
            0x20 or 0x0D or 0x08 or 0xA0 or 0xA1 => pack.Big,
            _ => pack.Normal[_random.Next(pack.Normal.Length)],
        };
        _engine.Play(sound, (float)settings.Volume, Pan(vk));
    }

    public void OnKeyUp(int vk)
    {
        if (settings.KeyUpSounds && ShouldPlay())
            _engine.Play(_pack!.Up, (float)settings.Volume, Pan(vk));
    }

    /// <summary>Plays a short demo so you can hear the pack.</summary>
    public async Task PreviewAsync()
    {
        bool wasOn = settings.Enabled;
        settings.Enabled = true;
        Apply();
        foreach (var vk in new[] { 0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x50, 0x4F, 0x54, 0x41, 0x54, 0x4F, 0x0D })
        {
            OnKeyDown(vk);
            await Task.Delay(vk == 0x20 ? 160 : 95);
            OnKeyUp(vk);
        }
        await Task.Delay(400);
        settings.Enabled = wasOn;
        Apply();
    }

    bool ShouldPlay() =>
        settings.Enabled && _pack != null && _engine.IsRunning && !(settings.MuteInGamerMode && gamerModeActive());

    // Keys on the left sound a bit more on the left, like a real keyboard in front of you.
    static float Pan(int vk)
    {
        const string rows = "QWERTYUIOP ASDFGHJKL ZXCVBNM";
        int i = vk is >= 0x41 and <= 0x5A ? rows.IndexOf((char)vk) : -1;
        if (i < 0) return 0;
        double col = i switch { < 10 => i / 9.0, < 20 => (i - 11) / 8.0, _ => (i - 20) / 6.0 };
        return (float)((col - 0.5) * 0.5);
    }

    public void Dispose() => _engine.Dispose();
}
