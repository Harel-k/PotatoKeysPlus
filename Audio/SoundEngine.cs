using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace PotatoKeysPlus.Audio;

/// <summary>
/// Low-latency mixer: every sound gets its own voice, so fast typing never cuts sounds off.
/// The audio device is only opened while key sounds are on.
/// </summary>
public sealed class SoundEngine : IDisposable
{
    WasapiOut? _output;
    MixingSampleProvider? _mixer;

    public int SampleRate { get; private set; } = 48000;
    public bool IsRunning => _output != null;

    public void Start()
    {
        if (_output != null) return;
        using var devices = new MMDeviceEnumerator();
        var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        SampleRate = device.AudioClient.MixFormat.SampleRate;
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2)) { ReadFully = true };
        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 25);
        _output.Init(_mixer);
        _output.Play();
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _mixer = null;
    }

    public void Play(float[] mono, float volume, float pan = 0)
    {
        var mixer = _mixer;
        if (mixer == null || mixer.MixerInputs.Count() > 48) return;
        mixer.AddMixerInput(new Voice(mono, volume, pan, mixer.WaveFormat));
    }

    public void Dispose() => Stop();

    sealed class Voice(float[] mono, float volume, float pan, WaveFormat format) : ISampleProvider
    {
        int _pos;
        readonly float _left = volume * (pan <= 0 ? 1 : 1 - pan);
        readonly float _right = volume * (pan >= 0 ? 1 : 1 + pan);

        public WaveFormat WaveFormat => format;

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = Math.Min(count / 2, mono.Length - _pos);
            for (int i = 0; i < frames; i++)
            {
                float s = mono[_pos++];
                buffer[offset + i * 2] = s * _left;
                buffer[offset + i * 2 + 1] = s * _right;
            }
            return frames * 2;
        }
    }
}
