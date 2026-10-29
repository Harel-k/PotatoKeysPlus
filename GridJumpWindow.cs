using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

/// <summary>The Grid Jump overlay. Click-through, never takes focus.</summary>
public sealed class GridJumpWindow : Window, GridJumpModule.IView
{
    readonly Canvas _canvas = new();
    readonly List<(int Row, Border Label)> _labels = [];
    int _rows, _cols;

    public GridJumpWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0x40, 0x0E, 0x0F, 0x12));
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Cascadia Mono, Consolas");
        Content = _canvas;
        SizeChanged += (_, _) => Build();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT);
    }

    public void Open(int left, int top, int width, int height, int rows, int cols)
    {
        _rows = rows;
        _cols = cols;
        if (!IsVisible) Show();
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, left, top, width, height,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        Build();
    }

    void Build()
    {
        _canvas.Children.Clear();
        _labels.Clear();
        double w = ActualWidth, h = ActualHeight;
        if (_rows == 0 || w <= 0) return;
        double cw = w / _cols, ch = h / _rows;
        var line = new SolidColorBrush(Color.FromArgb(0x55, 0xF4, 0xB9, 0x42));

        for (int c = 1; c < _cols; c++)
            _canvas.Children.Add(new Line { X1 = c * cw, X2 = c * cw, Y1 = 0, Y2 = h, Stroke = line, StrokeThickness = 1 });
        for (int r = 1; r < _rows; r++)
            _canvas.Children.Add(new Line { X1 = 0, X2 = w, Y1 = r * ch, Y2 = r * ch, Stroke = line, StrokeThickness = 1 });

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var text = new TextBlock
                {
                    FontSize = Math.Clamp(ch / 4.2, 10, 18), FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                };
                text.Inlines.Add(new System.Windows.Documents.Run(((char)('A' + r)).ToString()) { Foreground = (Brush)FindResource("AccentBrush") });
                text.Inlines.Add(new System.Windows.Documents.Run(((char)('A' + c)).ToString()));
                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x18, 0x19, 0x1E)),
                    CornerRadius = new CornerRadius(5), Padding = new Thickness(5, 1, 5, 2), Child = text,
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, c * cw + (cw - label.DesiredSize.Width) / 2);
                Canvas.SetTop(label, r * ch + (ch - label.DesiredSize.Height) / 2);
                _canvas.Children.Add(label);
                _labels.Add((r, label));
            }
        }

        var hint = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x18, 0x19, 0x1E)), CornerRadius = new CornerRadius(14),
            BorderBrush = (Brush)FindResource("AccentBrush"), BorderThickness = new Thickness(1.5), Padding = new Thickness(16, 7, 16, 8),
            Child = new TextBlock
            {
                Text = "Type the row letter, then the column letter   ·   Backspace = back   ·   Esc = cancel",
                Foreground = Brushes.White, FontSize = 13, FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            },
        };
        hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(hint, (w - hint.DesiredSize.Width) / 2);
        Canvas.SetTop(hint, h - hint.DesiredSize.Height - 40);
        _canvas.Children.Add(hint);
    }

    public void ShowRow(int row)
    {
        foreach (var (r, label) in _labels)
        {
            label.Opacity = row < 0 || r == row ? 1 : 0.18;
            label.BorderBrush = r == row ? (Brush)FindResource("AccentBrush") : null;
            label.BorderThickness = new Thickness(r == row ? 1.5 : 0);
        }
    }

    public new void Close() => Hide();
}
