using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Grid Jump: a lettered grid covers the screen. Type the row letter then the column letter
/// and the mouse jumps to that cell. Much faster than moving with the arrows.
/// </summary>
public sealed class GridJumpModule(GridJumpSettings settings, Engine engine)
{
    public interface IView
    {
        void Open(int left, int top, int width, int height, int rows, int cols);
        void ShowRow(int row);
        void Close();
    }

    Native.RECT _screen;
    int _rows, _cols, _row = -1;

    public IView? View { get; set; }
    public bool IsOpen { get; private set; }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (IsOpen)
        {
            if (vk is >= 0x41 and <= 0x5A)
            {
                int n = vk - 0x41;
                if (_row < 0) { if (n < _rows) { _row = n; View?.ShowRow(n); } }
                else if (n < _cols) Jump(_row, n);
                return true;
            }
            if (vk == 0x08) { _row = -1; View?.ShowRow(-1); return true; }
            if (vk == 0x1B) { Close(); return true; }
            if (KeyNames.IsModifier(vk)) return false;
            Close();
            return false;
        }

        if (!settings.Enabled) return false;
        bool trigger = settings.Hotkey.Matches(vk, mods)
            || (engine.Mouse.Active && settings.MouseModeKey.IsSet && vk == settings.MouseModeKey.Vk && mods == settings.MouseModeKey.Mods);
        if (!trigger) return false;
        Open();
        return true;
    }

    public void Open()
    {
        Native.GetCursorPos(out var p);
        _screen = Native.MonitorInfo(Native.MonitorFromPoint(p, 2)).rcMonitor;
        double scale = Native.DpiScale(Native.MonitorFromPoint(p, 2));
        double cell = Math.Max(40, settings.CellSize) * scale;
        _cols = Math.Clamp((int)Math.Round(_screen.Width / cell), 4, 26);
        _rows = Math.Clamp((int)Math.Round(_screen.Height / cell), 3, 26);
        _row = -1;
        IsOpen = true;
        View?.Open(_screen.Left, _screen.Top, _screen.Width, _screen.Height, _rows, _cols);
    }

    void Jump(int row, int col)
    {
        int x = _screen.Left + (int)((col + 0.5) * _screen.Width / _cols);
        int y = _screen.Top + (int)((row + 0.5) * _screen.Height / _rows);
        Close();
        Native.SetCursorPos(x, y);
        if (settings.ClickAfterJump) InputSender.Click(ClickButton.Left);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        View?.Close();
    }
}
