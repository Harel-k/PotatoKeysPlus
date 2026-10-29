using System.Windows.Threading;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Mouse Jiggler: keeps the PC awake and your chat status "active" by nudging the mouse
/// one pixel and straight back, so the cursor doesn't visibly move.
/// </summary>
public sealed class MouseJigglerModule : IDisposable
{
    readonly JigglerSettings _s;
    readonly DispatcherTimer _timer;
    DateTime _startedAt, _lastJiggle;

    public bool Running { get; private set; }
    public int JiggleCount { get; private set; }
    public event Action? Changed;

    public MouseJigglerModule(JigglerSettings settings)
    {
        _s = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    public TimeSpan RunningFor => Running ? DateTime.Now - _startedAt : TimeSpan.Zero;

    public void SetRunning(bool run)
    {
        if (run && !_s.Enabled) run = false;
        if (run == Running) return;
        Running = run;
        if (run)
        {
            _startedAt = _lastJiggle = DateTime.Now;
            JiggleCount = 0;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
        ApplyKeepAwake();
        Changed?.Invoke();
    }

    public void ApplyKeepAwake()
    {
        uint flags = Native.ES_CONTINUOUS;
        if (Running) flags |= Native.ES_SYSTEM_REQUIRED | (_s.KeepScreenOn ? Native.ES_DISPLAY_REQUIRED : 0);
        Native.SetThreadExecutionState(flags);
    }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!_s.Enabled || !_s.Toggle.Matches(vk, mods)) return false;
        SetRunning(!Running);
        return true;
    }

    void Tick()
    {
        if (_s.StopAfterMinutes > 0 && RunningFor.TotalMinutes >= _s.StopAfterMinutes)
        {
            SetRunning(false);
            return;
        }

        var interval = Math.Max(5, _s.IntervalSeconds);
        if ((DateTime.Now - _lastJiggle).TotalSeconds < interval) return;
        _lastJiggle = DateTime.Now;

        // While you're actually using the PC there's nothing to do.
        if (_s.OnlyWhenIdle && Native.IdleSeconds() < interval - 1) return;

        InputSender.MouseMove(1, 0);
        InputSender.MouseMove(-1, 0);
        JiggleCount++;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();
        Running = false;
        Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
    }
}
