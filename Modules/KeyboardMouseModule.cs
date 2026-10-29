using System.Diagnostics;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Keyboard Mouse: while mouse mode is on, the bound keys move the cursor (with acceleration),
/// click and scroll. Key handling runs on the UI thread; movement runs on its own timing thread.
/// </summary>
public sealed class KeyboardMouseModule : IDisposable
{
    enum Act { None, Up, Down, Left, Right, LeftClick, RightClick, MiddleClick, ScrollUp, ScrollDown, ScrollLeft, ScrollRight, Boost, Precision }

    readonly MouseSettings _s;
    readonly Thread _thread;
    volatile bool _stop;
    volatile bool _active;
    volatile bool _up, _down, _left, _right, _boost, _precise;
    volatile bool _scrollUp, _scrollDown, _scrollLeft, _scrollRight;
    readonly HashSet<ClickButton> _heldButtons = [];

    public event Action<bool>? ActiveChanged;

    public KeyboardMouseModule(MouseSettings settings)
    {
        _s = settings;
        _thread = new Thread(Loop) { IsBackground = true, Name = "PotatoKeys mouse loop", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public bool Active => _active;

    public void SetActive(bool value)
    {
        if (value && !_s.Enabled) value = false;
        if (_active == value) return;
        _active = value;
        if (!value) ReleaseAll();
        ActiveChanged?.Invoke(value);
    }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!_s.Enabled) return false;

        var toggle = _s.Toggle;
        if (toggle.IsSet && vk == toggle.Vk && (toggle.Mods == Mods.None || toggle.Mods == mods))
        {
            SetActive(!_active);
            return true;
        }
        if (!_active) return false;

        var act = Lookup(vk);
        switch (act)
        {
            case Act.None: return false;
            case Act.Up: _up = true; break;
            case Act.Down: _down = true; break;
            case Act.Left: _left = true; break;
            case Act.Right: _right = true; break;
            case Act.Boost: _boost = true; break;
            case Act.Precision: _precise = true; break;
            case Act.LeftClick: Press(ClickButton.Left); break;
            case Act.RightClick: Press(ClickButton.Right); break;
            case Act.MiddleClick: Press(ClickButton.Middle); break;
            case Act.ScrollUp: InputSender.Wheel(120); _scrollUp = true; break;
            case Act.ScrollDown: InputSender.Wheel(-120); _scrollDown = true; break;
            case Act.ScrollLeft: InputSender.HWheel(-120); _scrollLeft = true; break;
            case Act.ScrollRight: InputSender.HWheel(120); _scrollRight = true; break;
        }
        // Modifier keys (Shift/Ctrl as boost/precision) are only watched, never blocked.
        return _s.BlockKeys && !KeyNames.IsModifier(vk);
    }

    public void OnKeyUp(int vk)
    {
        switch (Lookup(vk))
        {
            case Act.Up: _up = false; break;
            case Act.Down: _down = false; break;
            case Act.Left: _left = false; break;
            case Act.Right: _right = false; break;
            case Act.Boost: _boost = false; break;
            case Act.Precision: _precise = false; break;
            case Act.LeftClick: Release(ClickButton.Left); break;
            case Act.RightClick: Release(ClickButton.Right); break;
            case Act.MiddleClick: Release(ClickButton.Middle); break;
            case Act.ScrollUp: _scrollUp = false; break;
            case Act.ScrollDown: _scrollDown = false; break;
            case Act.ScrollLeft: _scrollLeft = false; break;
            case Act.ScrollRight: _scrollRight = false; break;
        }
    }

    Act Lookup(int vk)
    {
        if (vk == _s.Up.Vk) return Act.Up;
        if (vk == _s.Down.Vk) return Act.Down;
        if (vk == _s.Left.Vk) return Act.Left;
        if (vk == _s.Right.Vk) return Act.Right;
        if (vk == _s.LeftClick.Vk) return Act.LeftClick;
        if (vk == _s.RightClick.Vk) return Act.RightClick;
        if (vk == _s.MiddleClick.Vk) return Act.MiddleClick;
        if (vk == _s.ScrollUp.Vk) return Act.ScrollUp;
        if (vk == _s.ScrollDown.Vk) return Act.ScrollDown;
        if (vk == _s.ScrollLeft.Vk) return Act.ScrollLeft;
        if (vk == _s.ScrollRight.Vk) return Act.ScrollRight;
        if (vk == _s.Boost.Vk) return Act.Boost;
        if (vk == _s.Precision.Vk) return Act.Precision;
        return Act.None;
    }

    // Holding the click key holds the mouse button, so you can drag things.
    void Press(ClickButton b)
    {
        if (_heldButtons.Add(b)) InputSender.MouseButton(b, true);
    }

    void Release(ClickButton b)
    {
        if (_heldButtons.Remove(b)) InputSender.MouseButton(b, false);
    }

    void ReleaseAll()
    {
        _up = _down = _left = _right = _boost = _precise = false;
        _scrollUp = _scrollDown = _scrollLeft = _scrollRight = false;
        foreach (var b in _heldButtons.ToArray()) Release(b);
    }

    void Loop()
    {
        Native.timeBeginPeriod(1);
        try
        {
            var clock = Stopwatch.StartNew();
            double last = 0, held = 0, accX = 0, accY = 0, scrollHeld = 0, scrollAcc = 0;

            while (!_stop)
            {
                Thread.Sleep(4);
                double now = clock.Elapsed.TotalSeconds;
                double dt = Math.Min(now - last, 0.05);
                last = now;

                if (!_active)
                {
                    held = accX = accY = scrollHeld = scrollAcc = 0;
                    continue;
                }

                int dx = (_right ? 1 : 0) - (_left ? 1 : 0);
                int dy = (_down ? 1 : 0) - (_up ? 1 : 0);
                if (dx != 0 || dy != 0)
                {
                    held += dt;
                    double t = _s.AccelTime <= 0 ? 1 : Math.Min(held / _s.AccelTime, 1);
                    t = t * t * (3 - 2 * t); // smooth ease-in
                    double speed = _s.StartSpeed + (Math.Max(_s.MaxSpeed, _s.StartSpeed) - _s.StartSpeed) * t;
                    if (_boost) speed *= _s.BoostMultiplier;
                    if (_precise) speed *= _s.PrecisionMultiplier;

                    double len = Math.Sqrt(dx * dx + dy * dy);
                    accX += dx / len * speed * dt;
                    accY += dy / len * speed * dt;
                    int mx = (int)accX, my = (int)accY;
                    if ((mx != 0 || my != 0) && Native.GetCursorPos(out var p))
                    {
                        accX -= mx;
                        accY -= my;
                        Native.SetCursorPos(p.X + mx, p.Y + my);
                    }
                }
                else
                {
                    held = accX = accY = 0;
                }

                int sv = (_scrollUp ? 1 : 0) - (_scrollDown ? 1 : 0);
                int sh = (_scrollRight ? 1 : 0) - (_scrollLeft ? 1 : 0);
                if (sv != 0 || sh != 0)
                {
                    // The first notch is sent on key press; keep scrolling after a short delay.
                    scrollHeld += dt;
                    if (scrollHeld > 0.3)
                    {
                        scrollAcc += _s.ScrollSpeed * dt;
                        while (scrollAcc >= 1)
                        {
                            scrollAcc -= 1;
                            if (sv != 0) InputSender.Wheel(sv * 120);
                            if (sh != 0) InputSender.HWheel(sh * 120);
                        }
                    }
                }
                else
                {
                    scrollHeld = scrollAcc = 0;
                }
            }
        }
        finally
        {
            Native.timeEndPeriod(1);
        }
    }

    public void Dispose()
    {
        _active = false;
        ReleaseAll();
        _stop = true;
        _thread.Join(500);
    }
}
