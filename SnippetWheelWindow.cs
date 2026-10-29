using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

/// <summary>
/// The round Snippet Wheel. It never takes focus, so text and keys still go to the app you were using.
/// </summary>
public sealed class SnippetWheelWindow : Window, SnippetWheelModule.IView
{
    const double Size = 400, OuterR = 185, InnerR = 64;

    readonly SnippetWheelModule _module;
    readonly Canvas _canvas = new() { Width = Size, Height = Size };
    readonly List<Path> _slices = [];
    readonly TextBlock _centerText;
    IReadOnlyList<HotkeyEntry> _items = [];

    public SnippetWheelWindow(SnippetWheelModule module)
    {
        _module = module;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Width = Height = Size;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        _centerText = new TextBlock
        {
            Width = InnerR * 2 - 16, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush("TextBrush"),
        };
        Content = _canvas;
    }

    Brush Brush(string key) => (Brush)FindResource(key);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
    }

    public void Open(IReadOnlyList<HotkeyEntry> items)
    {
        _items = items;
        Build();
        Opacity = 0;
        Show();

        // Centre on the mouse, in physical pixels (works across monitors with different scaling).
        Native.GetCursorPos(out var p);
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)(Size * dpi.DpiScaleX), h = (int)(Size * dpi.DpiScaleY);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, p.X - w / 2, p.Y - h / 2, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90)));
    }

    public void Select(int index)
    {
        for (int i = 0; i < _slices.Count; i++)
            _slices[i].Fill = i == index ? Brush("AccentBrush") : new SolidColorBrush(Color.FromArgb(0xF2, 0x24, 0x27, 0x2F));
        _centerText.Text = index >= 0 && index < _items.Count ? _items[index].Name : "Pick one\n1-8 · arrows";
        _centerText.Foreground = Brush(index >= 0 ? "AccentHoverBrush" : "MutedBrush");
    }

    public new void Close()
    {
        BeginAnimation(OpacityProperty, null);
        Hide();
    }

    void Build()
    {
        _canvas.Children.Clear();
        _slices.Clear();
        int n = Math.Max(_items.Count, 1);
        double step = 360.0 / n, c = Size / 2;

        for (int i = 0; i < _items.Count; i++)
        {
            double mid = -90 + i * step;
            double a0 = mid - step / 2 + 1.2, a1 = mid + step / 2 - 1.2;
            var slice = new Path
            {
                Data = Sector(c, InnerR, OuterR, a0, a1),
                Stroke = new SolidColorBrush(Color.FromArgb(0x90, 0xF4, 0xB9, 0x42)),
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
            };
            int index = i;
            slice.MouseEnter += (_, _) => _module.Select(index);
            slice.MouseLeftButtonUp += (_, _) => _module.Fire(index);
            _canvas.Children.Add(slice);
            _slices.Add(slice);

            // Number + name, centred in the slice
            double r = (InnerR + OuterR) / 2, rad = mid * Math.PI / 180;
            var label = new StackPanel { Width = 104, IsHitTestVisible = false };
            label.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            label.Children.Add(new TextBlock
            {
                Text = _items[i].Name, FontSize = 12.5, Foreground = Brush("TextBrush"), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxHeight = 36, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Canvas.SetLeft(label, c + r * Math.Cos(rad) - 52);
            Canvas.SetTop(label, c + r * Math.Sin(rad) - 20);
            _canvas.Children.Add(label);
        }

        var hub = new Ellipse
        {
            Width = (InnerR - 6) * 2, Height = (InnerR - 6) * 2,
            Fill = new SolidColorBrush(Color.FromArgb(0xF5, 0x18, 0x19, 0x1E)),
            Stroke = Brush("AccentBrush"), StrokeThickness = 1.5, IsHitTestVisible = false,
        };
        Canvas.SetLeft(hub, c - hub.Width / 2);
        Canvas.SetTop(hub, c - hub.Height / 2);
        _canvas.Children.Add(hub);

        Canvas.SetLeft(_centerText, c - _centerText.Width / 2);
        Canvas.SetTop(_centerText, c - 16);
        _canvas.Children.Add(_centerText);

        Select(-1);
    }

    static Geometry Sector(double c, double rIn, double rOut, double a0, double a1)
    {
        Point P(double r, double a) => new(c + r * Math.Cos(a * Math.PI / 180), c + r * Math.Sin(a * Math.PI / 180));
        bool large = a1 - a0 > 180;
        var figure = new PathFigure { StartPoint = P(rOut, a0), IsClosed = true };
        figure.Segments.Add(new ArcSegment(P(rOut, a1), new System.Windows.Size(rOut, rOut), 0, large, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(P(rIn, a1), true));
        figure.Segments.Add(new ArcSegment(P(rIn, a0), new System.Windows.Size(rIn, rIn), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry([figure]);
    }
}
