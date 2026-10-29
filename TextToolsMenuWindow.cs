using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

/// <summary>The Text Tools pick-list. Never takes focus, so the selected text stays selected.</summary>
public sealed class TextToolsMenuWindow : Window, TextToolsModule.IMenuView
{
    readonly TextToolsModule _module;
    readonly StackPanel _list = new();

    public TextToolsMenuWindow(TextToolsModule module)
    {
        _module = module;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var title = new TextBlock { Text = "Text Tools", FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(10, 2, 10, 6) };
        var body = new StackPanel { MinWidth = 230 };
        body.Children.Add(title);
        body.Children.Add(_list);
        body.Children.Add(new TextBlock { Text = "1-8 or click  ·  Esc to close", FontSize = 11, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(10, 6, 10, 2) });
        Content = new Border
        {
            Margin = new Thickness(14), Padding = new Thickness(6, 8, 6, 8), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(0xF7, 0x1A, 0x1C, 0x22)), BorderBrush = (Brush)FindResource("AccentBrush"),
            BorderThickness = new Thickness(1), Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 4, Opacity = 0.5 }, Child = body,
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
    }

    public void Open(IReadOnlyList<string> items)
    {
        _list.Children.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var num = new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentBrush") };
            var name = new TextBlock { Text = items[i], Foreground = (Brush)FindResource("TextBrush") };
            Grid.SetColumn(name, 1);
            row.Children.Add(num);
            row.Children.Add(name);
            var item = new Border { Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(7), Child = row, Cursor = Cursors.Hand, Background = Brushes.Transparent };
            item.MouseEnter += (_, _) => _module.Select(index);
            item.MouseLeftButtonUp += (_, _) => _module.Run(index);
            _list.Children.Add(item);
        }
        Select(-1);
        Show();
        UpdateLayout();
        Native.GetCursorPos(out var p);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, p.X + 8, p.Y + 8, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    public void Select(int index)
    {
        for (int i = 0; i < _list.Children.Count; i++)
            ((Border)_list.Children[i]).Background = i == index ? (Brush)FindResource("AccentDimBrush") : Brushes.Transparent;
    }

    public new void Close() => Hide();
}
