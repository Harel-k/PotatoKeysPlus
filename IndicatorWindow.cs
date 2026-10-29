using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using PotatoKeysPlus.Interop;

namespace PotatoKeysPlus;

/// <summary>Small click-through pill at the bottom of the screen that shows mouse mode is on.</summary>
public sealed class IndicatorWindow : Window
{
    readonly TextBlock _text;

    public IndicatorWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var accent = new SolidColorBrush(Color.FromRgb(0xF4, 0xB9, 0x42));
        _text = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xED, 0xEF)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = accent, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(_text);

        Content = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1A, 0x1C, 0x22)),
            BorderBrush = accent,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(4),
            Child = row,
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
    }

    public void ShowText(string text)
    {
        _text.Text = text;
        if (!IsVisible) Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight - 28;
        // Re-assert so we sit above windows that became topmost after us.
        Topmost = false;
        Topmost = true;
    }
}
