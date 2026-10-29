using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Threading;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

public partial class MainWindow : Window
{
    /// <summary>A sidebar entry. Live = "doing something right now" (gold dot).</summary>
    sealed record ModuleInfo(string Group, string Key, string Glyph, string Name, Func<bool>? Enabled, Func<bool>? Live, Func<FrameworkElement> Build);

    readonly Engine _e;
    readonly IndicatorWindow _indicator = new();
    readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    readonly Dictionary<string, FrameworkElement> _pages = [];
    readonly Dictionary<string, Ellipse> _dots = [];
    readonly List<(CheckBox Box, Func<bool> Get)> _switches = [];
    readonly List<Action> _refreshers = [];
    readonly List<ModuleInfo> _modules;
    readonly LauncherWindow _launcherWindow;
    readonly ScreenshotWindow _screenshotWindow;
    readonly QuickNotesWindow _notesWindow;
    bool _syncing;
    bool _quitting;
    TrayIcon? _tray;

    AppSettings S => _e.Settings;

    public MainWindow(Engine engine)
    {
        _e = engine;
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Potato Keys+  v{version?.ToString(3)}";

        _modules =
        [
            new("Mouse", "mouse", "", "Keyboard Mouse", () => S.Mouse.Enabled, () => _e.Mouse.Active, BuildMousePage),
            new("Mouse", "gridjump", "", "Grid Jump", () => S.GridJump.Enabled, null, BuildGridJumpPage),
            new("Keyboard", "hotkeys", "", "Hotkeys", () => S.Hotkeys.Enabled, null, BuildHotkeysPage),
            new("Keyboard", "snippets", "", "Snippet Wheel", () => S.Snippets.Enabled, null, BuildSnippetsPage),
            new("Keyboard", "symbollock", "", "Symbol Lock", () => S.SymbolLock.Enabled, () => _e.SymbolLock.Active, BuildSymbolLockPage),
            new("Keyboard", "gamer", "", "Gamer Mode", () => S.Gamer.Enabled, () => _e.Gamer.Active, BuildGamerPage),
            new("Keyboard", "panic", "", "Panic Key", () => S.Panic.Enabled, () => _e.Panic.Panicked, BuildPanicPage),
            new("Text", "expander", "", "Text Expander", () => S.Expander.Enabled, null, BuildExpanderPage),
            new("Text", "texttools", "", "Text Tools", () => S.TextTools.Enabled, null, BuildTextToolsPage),
            new("Text", "notes", "", "Quick Notes", () => S.Notes.Enabled, null, BuildNotesPage),
            new("Text", "typing", "", "Typing Trainer", () => S.Typing.Enabled, null, BuildTypingPage),
            new("Windows & screen", "launcher", "", "Quick Launcher", () => S.Launcher.Enabled, null, BuildLauncherPage),
            new("Windows & screen", "snapper", "", "Window Snapper", () => S.Snapper.Enabled, null, BuildSnapperPage),
            new("Windows & screen", "pin", "", "Pin Any Window", () => S.WindowPin.Enabled, () => _e.WindowPin.Pinned.Count > 0, BuildPinPage),
            new("Windows & screen", "screenshot", "", "Screenshot", () => S.Screenshot.Enabled, null, BuildScreenshotPage),
            new("Sound", "appvolume", "", "App Volume", () => S.AppVolume.Enabled, null, BuildAppVolumePage),
            new("Sound", "sounds", "", "Key Sounds", () => S.Sounds.Enabled, null, BuildSoundsPage),
            new("More", "jiggler", "", "Mouse Jiggler", () => S.Jiggler.Enabled, () => _e.Jiggler.Running, BuildJigglerPage),
            new("More", "about", "", "Settings & updates", null, null, BuildAboutPage),
        ];

        _launcherWindow = new LauncherWindow(_e.Launcher);
        _e.Launcher.OpenRequested += _launcherWindow.Open;
        _e.Snippets.View = new SnippetWheelWindow(_e.Snippets);
        _e.GridJump.View = new GridJumpWindow();
        _e.TextTools.View = new TextToolsMenuWindow(_e.TextTools);
        _screenshotWindow = new ScreenshotWindow(S.Screenshot, Toast);
        _e.Screenshot.OpenRequested += _screenshotWindow.Open;
        _notesWindow = new QuickNotesWindow(S.Notes, _e.SaveSoon);
        _e.Notes.ToggleRequested += _notesWindow.Toggle;
        AddLauncherCommands();

        BuildModuleList();
        BuildCompactView();
        ApplySavedBounds();
        SetPinned(S.Pinned, save: false);

        _e.Mouse.ActiveChanged += _ => RefreshStatus();
        _e.Gamer.Changed += () => { RefreshStatus(); Toast(_e.Gamer.Active ? "Gamer Mode on" : "Gamer Mode off"); };
        _e.Jiggler.Changed += RefreshStatus;
        _e.Panic.Changed += RefreshStatus;
        _e.WindowPin.Changed += RefreshStatus;
        _e.SymbolLock.Changed += () => { RefreshStatus(); if (!S.SymbolLock.ShowIndicator) Toast(_e.SymbolLock.Active ? "Symbol Lock on" : "Symbol Lock off"); };
        _e.Toast += Toast;
        _e.PinToggleRequested += () => SetPinned(!S.Pinned);
        _e.SafetyLockChanged += () =>
        {
            RefreshStatus();
            Toast(_e.SafetyLocked ? "Safety Lock on: every module is off" : "Safety Lock off: modules are back on");
        };
        _e.Error += message => MessageBox.Show(this, message, "Potato Keys+", MessageBoxButton.OK, MessageBoxImage.Warning);

        _tray = new TrayIcon(
            open: ShowFromTray,
            checkUpdates: () => { ShowFromTray(); SelectModule("about"); _ = CheckForUpdatesAsync(userInitiated: true); },
            quit: QuitApp);
        _tray.AddToggle("Safety Lock (all modules off)", () => _e.SafetyLocked, () =>
        {
            if (!_e.SafetyLocked) _e.SetSafetyLock(true);
            else { ShowFromTray(); Toast("Click Unlock in the window to switch modules back on"); }
        });
        _tray.AddAction("Quick Launcher", Guard(_launcherWindow.Open));
        _tray.AddAction("Quick Notes", Guard(_notesWindow.Toggle));
        _tray.AddAction("Take a screenshot", Guard(_screenshotWindow.Open));
        _tray.AddToggle("Mouse mode", () => _e.Mouse.Active, Guard(() => _e.Mouse.SetActive(!_e.Mouse.Active)));
        _tray.AddToggle("Symbol Lock", () => _e.SymbolLock.Active, Guard(() => _e.SymbolLock.SetActive(!_e.SymbolLock.Active)));
        _tray.AddToggle("Gamer Mode", () => _e.Gamer.Active, Guard(() => _e.Gamer.SetActive(!_e.Gamer.Active)));
        _tray.AddToggle("Mouse Jiggler", () => _e.Jiggler.Running, Guard(() => _e.Jiggler.SetRunning(!_e.Jiggler.Running)));
        _tray.AddToggle("Key Sounds", () => S.Sounds.Enabled, Guard(() => { S.Sounds.Enabled = !S.Sounds.Enabled; _e.Sounds.Apply(); Changed(); }));
        _tray.AddToggle("Pin window on top", () => S.Pinned, () => SetPinned(!S.Pinned));

        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); RefreshIndicator(); };

        SourceInitialized += (_, _) => RoundCorners();
        Loaded += (_, _) => { if (S.Compact) SetCompact(true, save: false); };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _indicator.Close();
            _launcherWindow.Close();
            if (!_e.SkipSaveOnExit) _notesWindow.Flush();
            _tray?.Dispose();
        };

        SelectModule(S.SelectedModule);
        RefreshStatus();
        StartUpdateChecks();
    }

    void AddLauncherCommands()
    {
        var c = _e.Launcher.Commands;
        c.Add(("Mouse mode", "Potato Keys+ · move the mouse with your keys", () => _e.Mouse.SetActive(!_e.Mouse.Active)));
        c.Add(("Gamer Mode", "Potato Keys+ · block Win / Alt+Tab", () => _e.Gamer.SetActive(!_e.Gamer.Active)));
        c.Add(("Mouse Jiggler", "Potato Keys+ · keep the PC awake", () => _e.Jiggler.SetRunning(!_e.Jiggler.Running)));
        c.Add(("Key Sounds", "Potato Keys+ · keyboard click sounds", () => { S.Sounds.Enabled = !S.Sounds.Enabled; _e.Sounds.Apply(); Changed(); }));
        c.Add(("Typing test", "Potato Keys+ · check your speed", () => { ShowFromTray(); SelectModule("typing"); }));
        c.Add(("Potato Keys+ settings", "Open the Potato Keys+ window", () => { ShowFromTray(); SelectModule("about"); }));
        c.Add(("Quick Notes", "Potato Keys+ · open your notes", _notesWindow.Toggle));
        c.Add(("Screenshot", "Potato Keys+ · grab part of the screen", _screenshotWindow.Open));
        c.Add(("Symbol Lock", "Potato Keys+ · number row types symbols", () => _e.SymbolLock.SetActive(!_e.SymbolLock.Active)));
        c.Add(("Lock PC", "Windows · lock the computer", () => Native.LockWorkStation()));
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
        string? group = null;
        foreach (var m in _modules)
        {
            if (m.Group != group)
            {
                group = m.Group;
                ModuleList.Items.Add(new ListBoxItem
                {
                    Content = group.ToUpperInvariant(), IsEnabled = false, Focusable = false, IsHitTestVisible = false,
                    FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Res("MutedBrush"),
                    Padding = new Thickness(12, ModuleList.Items.Count == 0 ? 0 : 12, 0, 3), Style = St("ModuleItem"),
                });
            }
            ModuleList.Items.Add(ModuleItem(m.Key, m.Glyph, m.Name, out var dot));
            _dots[m.Key] = dot;
            if (m.Enabled == null) dot.Visibility = Visibility.Collapsed;
        }
        _updateDot = _dots["about"];
        _updateDot.Fill = Res("AccentBrush");
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
        return new ListBoxItem { Style = St("ModuleItem"), Tag = key, Content = grid, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 2) };
    }

    void SelectModule(string key)
    {
        foreach (ListBoxItem item in ModuleList.Items)
            if (item.Tag as string == key) { ModuleList.SelectedItem = item; return; }
        ModuleList.SelectedIndex = 1;
    }

    void ModuleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedItem is not ListBoxItem { Tag: string key }) return;
        if (!_pages.TryGetValue(key, out var page))
        {
            page = (_modules.FirstOrDefault(m => m.Key == key) ?? _modules[0]).Build();
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
            hint.Text = S.Mouse.Enabled ? $"Press {S.Mouse.Toggle} or click here" : "Turn it on in the full window";
            big.Background = Res(on ? "AccentDimBrush" : "CardBrush");
            big.BorderBrush = Res(on ? "AccentBrush" : "CardBorderBrush");
            title.Foreground = Res(on ? "AccentBrush" : "TextBrush");
        });
        CompactPanel.Children.Add(big);

        // Quick switches for the things you flip most.
        var quick = new UniformGrid { Columns = 2, Margin = new Thickness(2, 14, 0, 0) };
        void Add(CheckBox box)
        {
            box.Margin = new Thickness(0, 0, 0, 10);
            quick.Children.Add(box);
        }
        Add(Switch("Gamer Mode", () => _e.Gamer.Active, v => _e.Gamer.SetActive(v)));
        Add(Switch("Jiggler", () => _e.Jiggler.Running, v => _e.Jiggler.SetRunning(v)));
        Add(Switch("Key Sounds", () => S.Sounds.Enabled, v => { S.Sounds.Enabled = v; _e.Sounds.Apply(); }));
        Add(Switch("Hotkeys", () => S.Hotkeys.Enabled, v => S.Hotkeys.Enabled = v));
        CompactPanel.Children.Add(quick);

        var launch = new Button { Style = St("GhostButton"), Content = $"Quick Launcher  ({S.Launcher.Hotkey})", HorizontalAlignment = HorizontalAlignment.Stretch };
        launch.Click += (_, _) => _launcherWindow.Open();
        _refreshers.Add(() => launch.Content = $"Quick Launcher  ({S.Launcher.Hotkey})");
        CompactPanel.Children.Add(launch);
    }

    // ---------- Status ----------

    void RefreshStatus()
    {
        _syncing = true;
        foreach (var (box, get) in _switches) box.IsChecked = get();
        _syncing = false;

        foreach (var refresh in _refreshers) refresh();

        foreach (var m in _modules)
        {
            if (m.Enabled == null) continue;
            bool enabled = m.Enabled(), live = m.Live?.Invoke() == true;
            _dots[m.Key].Fill = Res(!enabled || _e.SafetyLocked ? "RaisedBrush" : live ? "AccentBrush" : "GoodBrush");
        }

        var states = new List<string>();
        if (_e.SafetyLocked) states.Add("SAFETY LOCK");
        if (_e.Mouse.Active) states.Add("MOUSE MODE");
        if (_e.Gamer.Active) states.Add("GAMER MODE");
        if (_e.Jiggler.Running) states.Add("JIGGLING");
        if (_e.SymbolLock.Active) states.Add("SYMBOL LOCK");
        StatusPillText.Text = string.Join("  ·  ", states);
        MouseModePill.Visibility = states.Count > 0 && !S.Compact ? Visibility.Visible : Visibility.Collapsed;

        RefreshSafety();
        RefreshIndicator();
        _tray?.Refresh(states.Count > 0 ? "Potato Keys+ (" + string.Join(", ", states).ToLowerInvariant() + ")" : "Potato Keys+");
        RefreshUpdateBanner();
    }

    void RefreshIndicator()
    {
        if (_toastTimer.IsEnabled) return; // a toast is showing; it calls back here when it's done
        var parts = new List<string>();
        if (_e.Mouse.Active && S.Mouse.ShowIndicator) parts.Add($"Mouse mode on  ·  {S.Mouse.Toggle} to stop");
        if (_e.SymbolLock.Active && S.SymbolLock.ShowIndicator) parts.Add("Symbol Lock on  ·  1 → !");
        if (parts.Count > 0) _indicator.ShowText(string.Join("     ", parts));
        else if (_indicator.IsVisible) _indicator.Hide();
    }

    /// <summary>Brief on-screen message, e.g. when a hotkey switches Gamer Mode.</summary>
    void Toast(string text)
    {
        _indicator.ShowText(text);
        _toastTimer.Stop();
        _toastTimer.Start();
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

    // ---------- Safety Lock ----------

    void SafetyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_e.SafetyLocked)
        {
            _e.SetSafetyLock(true);
            return;
        }
        // Only a real click (or a real key press on the focused button) unlocks. Clicks sent by
        // other programs, scripts or UI Automation are refused.
        if (Native.CurrentInputIsPhysical()) _e.SetSafetyLock(false);
        else Toast("Unlock needs a real click with your own mouse");
    }

    void RefreshSafety()
    {
        bool locked = _e.SafetyLocked;
        SafetyBanner.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        SafetyButton.Content = locked ? "" : "";
        if (locked) SafetyButton.Foreground = Res("DangerBrush");
        else SafetyButton.ClearValue(ForegroundProperty);
        SafetyButton.ToolTip = locked
            ? "Safety Lock is on: every module is off. Click with your mouse to unlock."
            : $"Safety Lock: switch every module off ({S.SafetyKey})";
        PageHost.IsEnabled = !locked;
        CompactPanel.IsEnabled = !locked;
    }

    /// <summary>Tray actions do nothing while the Safety Lock is on (they open the window instead).</summary>
    Action Guard(Action action) => () =>
    {
        if (!_e.SafetyLocked) action();
        else { ShowFromTray(); Toast("Safety Lock is on. Click Unlock first"); }
    };

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
