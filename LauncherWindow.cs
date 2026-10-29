using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

/// <summary>The Quick Launcher search popup.</summary>
public sealed class LauncherWindow : Window
{
    readonly LauncherModule _launcher;
    readonly TextBox _box;
    readonly StackPanel _results = new();
    readonly TextBlock _empty;
    List<LaunchItem> _items = [];
    int _selected;

    public LauncherWindow(LauncherModule launcher)
    {
        _launcher = launcher;
        Title = "Potato Keys+ Quick Launcher";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 680;
        SizeToContent = SizeToContent.Height;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var searchIcon = new TextBlock
        {
            Text = "", FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 20,
            Foreground = Brush("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 14, 0),
        };
        _box = new TextBox
        {
            FontSize = 22, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = Brush("TextBrush"), CaretBrush = Brush("AccentBrush"), VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 6, 0, 6),
        };
        _box.TextChanged += (_, _) => Refresh();

        var searchRow = new DockPanel();
        DockPanel.SetDock(searchIcon, Dock.Left);
        searchRow.Children.Add(searchIcon);
        searchRow.Children.Add(_box);

        _empty = new TextBlock { Foreground = Brush("MutedBrush"), FontSize = 13, Margin = new Thickness(10, 10, 10, 6), Visibility = Visibility.Collapsed };
        var hint = new TextBlock
        {
            Text = "↑ ↓ to move   ·   Enter to open   ·   Esc to close",
            Foreground = Brush("MutedBrush"), FontSize = 11.5, Margin = new Thickness(8, 10, 8, 0),
        };

        var body = new StackPanel();
        body.Children.Add(searchRow);
        body.Children.Add(new Border { Height = 1, Background = Brush("CardBorderBrush"), Margin = new Thickness(0, 10, 0, 6) });
        body.Children.Add(_empty);
        body.Children.Add(_results);
        body.Children.Add(hint);

        Content = new Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(0xF7, 0x1A, 0x1C, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, 0xF4, 0xB9, 0x42)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.55, Color = Colors.Black },
            Child = body,
        };

        PreviewKeyDown += OnKey;
        Deactivated += (_, _) => Hide();
    }

    Brush Brush(string key) => (Brush)FindResource(key);

    public async void Open()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.18;
        _box.Text = "";
        Refresh();
        Show();
        Activate();
        Native.ForceForeground(new WindowInteropHelper(this).Handle);
        _box.Focus();
        Keyboard.Focus(_box);

        await _launcher.RefreshIndexAsync();
        if (IsVisible) Refresh();
    }

    void Refresh()
    {
        _items = _launcher.Search(_box.Text);
        _selected = 0;
        _results.Children.Clear();
        for (int i = 0; i < _items.Count; i++) _results.Children.Add(Row(_items[i], i));

        if (_items.Count == 0)
        {
            _empty.Text = _launcher.IsIndexing ? "Loading your apps…"
                : _box.Text.Length == 0 ? "Start typing to find apps, websites and commands. Try =12*7 for maths."
                : "Nothing found.";
            _empty.Visibility = Visibility.Visible;
        }
        else
        {
            _empty.Visibility = Visibility.Collapsed;
        }
        Highlight();
    }

    FrameworkElement Row(LaunchItem item, int index)
    {
        var (glyph, color) = item.Kind switch
        {
            LaunchKind.Calculator => ("", "#4CC38A"),
            LaunchKind.Website => ("", "#5AA9F4"),
            LaunchKind.Search => ("", "#5AA9F4"),
            LaunchKind.File => ("", "#C9A35A"),
            LaunchKind.Command => ("", "#F4B942"),
            _ => ("", AppColor(item.Title)),
        };
        var avatarText = new TextBlock
        {
            Text = glyph.Length > 0 ? glyph : item.Title[..1].ToUpperInvariant(),
            FontFamily = glyph.Length > 0 ? (FontFamily)FindResource("IconFont") : FontFamily,
            FontSize = glyph.Length > 0 ? 15 : 16, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var avatar = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 0, 14, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), Child = avatarText,
        };

        var title = new TextBlock { Text = item.Title, FontSize = 14.5, Foreground = Brush("TextBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
        var subtitle = new TextBlock { Text = item.Subtitle, FontSize = 11.5, Foreground = Brush("MutedBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(subtitle);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(text, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(text);

        var row = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 1, 0, 1), Child = grid, Cursor = Cursors.Hand };
        row.MouseEnter += (_, _) => { _selected = index; Highlight(); };
        row.MouseLeftButtonUp += (_, _) => RunSelected();
        return row;
    }

    static string AppColor(string name)
    {
        string[] palette = ["#7C6CF2", "#E66A8A", "#3FA7A0", "#D9822B", "#4F8FE6", "#A55EEA", "#2EA160", "#C44D4D"];
        int hash = name.Aggregate(17, (h, c) => h * 31 + c);
        return palette[(hash & 0x7FFFFFFF) % palette.Length];
    }

    void Highlight()
    {
        for (int i = 0; i < _results.Children.Count; i++)
            ((Border)_results.Children[i]).Background = i == _selected ? Brush("AccentDimBrush") : Brushes.Transparent;
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Down or Key.Tab when _items.Count > 0:
                _selected = (_selected + 1) % _items.Count;
                Highlight();
                e.Handled = true;
                break;
            case Key.Up when _items.Count > 0:
                _selected = (_selected - 1 + _items.Count) % _items.Count;
                Highlight();
                e.Handled = true;
                break;
            case Key.Enter:
                RunSelected();
                e.Handled = true;
                break;
        }
    }

    void RunSelected()
    {
        if (_selected < 0 || _selected >= _items.Count) return;
        var item = _items[_selected];
        Hide();
        _launcher.Run(item);
    }
}
