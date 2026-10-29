using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

public partial class MainWindow : Window
{
    readonly Engine _e;
    readonly IndicatorWindow _indicator = new();
    readonly Dictionary<string, FrameworkElement> _pages = [];
    readonly List<(CheckBox Box, Func<bool> Get)> _switches = [];
    readonly List<Action> _refreshers = [];
    bool _syncing;
    bool _quitting;
    Ellipse? _mouseDot, _hotkeysDot;
    TrayIcon? _tray;

    AppSettings S => _e.Settings;

    public MainWindow(Engine engine)
    {
        _e = engine;
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Potato Keys+  v{version?.ToString(3)}";

        BuildModuleList();
        BuildCompactView();
        ApplySavedBounds();
        SetPinned(S.Pinned, save: false);

        _e.Mouse.ActiveChanged += _ => RefreshStatus();
        _e.PinToggleRequested += () => SetPinned(!S.Pinned);
        _e.Error += message => MessageBox.Show(this, message, "Potato Keys+", MessageBoxButton.OK, MessageBoxImage.Warning);

        _tray = new TrayIcon(
            open: ShowFromTray,
            toggleMouse: () => _e.Mouse.SetActive(!_e.Mouse.Active),
            togglePin: () => SetPinned(!S.Pinned),
            checkUpdates: () => { ShowFromTray(); SelectModule("about"); _ = CheckForUpdatesAsync(userInitiated: true); },
            quit: QuitApp);

        SourceInitialized += (_, _) => RoundCorners();
        Loaded += (_, _) => { if (S.Compact) SetCompact(true, save: false); };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _indicator.Close();
            _tray?.Dispose();
        };

        SelectModule(S.SelectedModule);
        RefreshStatus();
        StartUpdateChecks();
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveBounds();
        if (_quitting || !S.CloseToTray) return;

        // The close button hides to the tray so hotkeys and the keyboard mouse keep working.
        e.Cancel = true;
        Hide();
        if (!S.TrayHintShown)
        {
            S.TrayHintShown = true;
            _e.SaveSoon();
            _tray?.Notify("Potato Keys+ is still running", "Your hotkeys keep working. Click the potato in the tray to open it, or right-click it to quit.");
        }
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void QuitApp()
    {
        _quitting = true;
        Close();
    }

    // ---------- Sidebar ----------

    void BuildModuleList()
    {
        ModuleList.Items.Add(ModuleItem("mouse", "", "Keyboard Mouse", out _mouseDot));
        ModuleList.Items.Add(ModuleItem("hotkeys", "", "Hotkeys", out _hotkeysDot));
        ModuleList.Items.Add(ModuleItem("ideas", "", "More modules", out var ideasDot));
        ideasDot.Visibility = Visibility.Collapsed;
        ModuleList.Items.Add(ModuleItem("about", "", "Settings & updates", out _updateDot));
        _updateDot.Fill = Res("AccentBrush");
        _updateDot.Visibility = Visibility.Collapsed;
    }

    ListBoxItem ModuleItem(string key, string glyph, string name, out Ellipse dot)
    {
        var grid = Cols(GridLength.Auto, Star, GridLength.Auto);
        var icon = new TextBlock { Text = glyph, FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"), FontSize = 16, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = name, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
        dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        Place(grid, icon, 0);
        Place(grid, label, 1);
        Place(grid, dot, 2);
        return new ListBoxItem { Style = St("ModuleItem"), Tag = key, Content = grid, Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 2) };
    }

    void SelectModule(string key)
    {
        foreach (ListBoxItem item in ModuleList.Items)
            if ((string)item.Tag == key) { ModuleList.SelectedItem = item; return; }
        ModuleList.SelectedIndex = 0;
    }

    void ModuleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedItem is not ListBoxItem { Tag: string key }) return;
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "hotkeys" => BuildHotkeysPage(),
                "ideas" => BuildIdeasPage(),
                "about" => BuildAboutPage(),
                _ => BuildMousePage(),
            };
            _pages[key] = page;
            RefreshStatus();
        }
        PageHost.Content = page;
        PageScroller.ScrollToTop();
        if (S.SelectedModule != key) { S.SelectedModule = key; _e.SaveSoon(); }
    }

    // ---------- Compact widget ----------

    void BuildCompactView()
    {
        var title = Label("", 15, bold: true);
        var hint = Label("", 12, muted: true);
        hint.Margin = new Thickness(0, 3, 0, 0);
        var body = new StackPanel();
        body.Children.Add(title);
        body.Children.Add(hint);

        var big = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = body,
            ToolTip = "Click to toggle mouse mode",
        };
        big.MouseLeftButtonUp += (_, _) => _e.Mouse.SetActive(!_e.Mouse.Active);

        _refreshers.Add(() =>
        {
            bool on = _e.Mouse.Active;
            title.Text = !S.Mouse.Enabled ? "Keyboard Mouse is off" : on ? "Mouse mode: ON" : "Mouse mode: off";
            hint.Text = S.Mouse.Enabled ? $"Press {S.Mouse.Toggle} or click here" : "Turn it on below";
            big.Background = Res(on ? "AccentDimBrush" : "CardBrush");
            big.BorderBrush = Res(on ? "AccentBrush" : "CardBorderBrush");
            title.Foreground = Res(on ? "AccentBrush" : "TextBrush");
        });

        CompactPanel.Children.Add(big);

        var mouseRow = Switch("Keyboard Mouse", () => S.Mouse.Enabled, v => { S.Mouse.Enabled = v; if (!v) _e.Mouse.SetActive(false); });
        mouseRow.Margin = new Thickness(2, 14, 0, 0);
        CompactPanel.Children.Add(mouseRow);

        var hotkeysRow = Switch("Hotkeys", () => S.Hotkeys.Enabled, v => S.Hotkeys.Enabled = v);
        hotkeysRow.Margin = new Thickness(2, 10, 0, 0);
        CompactPanel.Children.Add(hotkeysRow);
    }

    // ---------- Status ----------

    void RefreshStatus()
    {
        _syncing = true;
        foreach (var (box, get) in _switches) box.IsChecked = get();
        _syncing = false;

        foreach (var refresh in _refreshers) refresh();

        bool on = _e.Mouse.Active;
        if (_mouseDot != null) _mouseDot.Fill = Res(!S.Mouse.Enabled ? "RaisedBrush" : on ? "AccentBrush" : "GoodBrush");
        if (_hotkeysDot != null) _hotkeysDot.Fill = Res(S.Hotkeys.Enabled ? "GoodBrush" : "RaisedBrush");
        MouseModePill.Visibility = on && !S.Compact ? Visibility.Visible : Visibility.Collapsed;

        if (on && S.Mouse.ShowIndicator) _indicator.ShowText($"Mouse mode on  ·  {S.Mouse.Toggle} to stop");
        else if (_indicator.IsVisible) _indicator.Hide();

        _tray?.SetState(on, S.Pinned);
        RefreshUpdateBanner();
    }

    void Changed()
    {
        _e.SaveSoon();
        RefreshStatus();
    }

    // ---------- Title bar ----------

    void PinButton_Changed(object sender, RoutedEventArgs e)
    {
        bool pinned = PinButton.IsChecked == true;
        if (pinned != S.Pinned) SetPinned(pinned);
    }

    void CompactButton_Click(object sender, RoutedEventArgs e) => SetCompact(!S.Compact);

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void SetPinned(bool pinned, bool save = true)
    {
        S.Pinned = pinned;
        Topmost = pinned;
        PinButton.IsChecked = pinned;
        PinButton.Content = pinned ? "" : "";
        PinButton.ToolTip = pinned ? "Pinned on top — click to unpin" : "Pin on top of every window";
        if (save) Changed();
    }

    void SetCompact(bool compact, bool save = true)
    {
        if (compact && !S.Compact)
        {
            S.WindowWidth = ActualWidth;
            S.WindowHeight = ActualHeight;
        }
        S.Compact = compact;

        FullView.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactView.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        CompactButton.Content = compact ? "" : "";
        CompactButton.ToolTip = compact ? "Back to the full window" : "Shrink to a mini widget";

        if (compact)
        {
            WindowState = WindowState.Normal;
            MinWidth = 340;
            MinHeight = 0;
            Width = 350;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanMinimize;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 760;
            MinHeight = 480;
            Width = Math.Max(S.WindowWidth, MinWidth);
            Height = Math.Max(S.WindowHeight, MinHeight);
        }
        if (save) Changed(); else RefreshStatus();
    }

    // ---------- Window placement ----------

    void ApplySavedBounds()
    {
        Width = Math.Max(S.WindowWidth, MinWidth);
        Height = Math.Max(S.WindowHeight, MinHeight);
        if (S.WindowLeft is double left && S.WindowTop is double top
            && left >= SystemParameters.VirtualScreenLeft - 50
            && top >= SystemParameters.VirtualScreenTop - 10
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    void SaveBounds()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        S.WindowLeft = bounds.Left;
        S.WindowTop = bounds.Top;
        if (!S.Compact)
        {
            S.WindowWidth = bounds.Width;
            S.WindowHeight = bounds.Height;
        }
        _e.SaveNow();
    }

    void RoundCorners()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int round = 2; // DWMWCP_ROUND (Windows 11; ignored on Windows 10)
        Native.DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
    }
}
