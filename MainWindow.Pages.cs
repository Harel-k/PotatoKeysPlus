using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus;

// Module pages, built in code so rows can be generated from the settings.
public partial class MainWindow
{
    static readonly GridLength Star = new(1, GridUnitType.Star);

    static readonly string[] ActionLabels =
    [
        "Type text",
        "Press other keys (remap)",
        "Open app, file or website",
        "Media control",
        "Mouse click",
        "Toggle mouse mode",
        "Pin / unpin this window",
    ];

    readonly StackPanel _hotkeyList = new();

    // ---------- Keyboard Mouse ----------

    FrameworkElement BuildMousePage()
    {
        var m = S.Mouse;
        var page = new StackPanel();

        page.Children.Add(PageHeader("Keyboard Mouse",
            "No mouse? No problem. Turn on mouse mode and drive the cursor with your keyboard: move, click, drag and scroll. Every key can be changed.",
            Switch("Enabled", () => m.Enabled, v => { m.Enabled = v; if (!v) _e.Mouse.SetActive(false); })));

        // Live status
        var dot = new Ellipse { Width = 12, Height = 12, Margin = new Thickness(2, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = Label("", 17, bold: true);
        var hint = Label("", 13, muted: true);
        hint.Margin = new Thickness(0, 3, 16, 0);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(hint);
        var toggle = new Button { Style = St("AccentButton"), MinWidth = 120, VerticalAlignment = VerticalAlignment.Center };
        toggle.Click += (_, _) => _e.Mouse.SetActive(!_e.Mouse.Active);

        var status = Cols(GridLength.Auto, Star, GridLength.Auto);
        Place(status, dot, 0);
        Place(status, text, 1);
        Place(status, toggle, 2);
        var statusCard = new Border { Style = St("Card"), Child = status };
        page.Children.Add(statusCard);

        _refreshers.Add(() =>
        {
            bool on = _e.Mouse.Active;
            title.Text = !m.Enabled ? "Keyboard Mouse is turned off" : on ? "Mouse mode is ON" : "Mouse mode is off";
            hint.Text = !m.Enabled ? "Flip the Enabled switch to use it."
                : on ? $"Your keys are driving the mouse. Press {m.Toggle} to get them back."
                : $"Press {m.Toggle} anywhere in Windows to start moving the mouse with your keys.";
            toggle.Content = on ? "Turn off" : "Turn on";
            toggle.IsEnabled = m.Enabled;
            dot.Fill = Res(on ? "AccentBrush" : m.Enabled ? "GoodBrush" : "RaisedBrush");
            statusCard.BorderBrush = Res(on ? "AccentBrush" : "CardBorderBrush");
        });

        // Keybinds
        var left = new StackPanel();
        left.Children.Add(SectionLabel("Mouse mode", first: true));
        left.Children.Add(KeyRow("Turn on / off", () => m.Toggle, v => m.Toggle = v, combo: true));
        left.Children.Add(SectionLabel("Move"));
        left.Children.Add(KeyRow("Up", () => m.Up, v => m.Up = v));
        left.Children.Add(KeyRow("Down", () => m.Down, v => m.Down = v));
        left.Children.Add(KeyRow("Left", () => m.Left, v => m.Left = v));
        left.Children.Add(KeyRow("Right", () => m.Right, v => m.Right = v));
        left.Children.Add(SectionLabel("Speed (hold)"));
        left.Children.Add(KeyRow("Boost (faster)", () => m.Boost, v => m.Boost = v));
        left.Children.Add(KeyRow("Precision (slower)", () => m.Precision, v => m.Precision = v));

        var right = new StackPanel();
        right.Children.Add(SectionLabel("Click (hold to drag)", first: true));
        right.Children.Add(KeyRow("Left click", () => m.LeftClick, v => m.LeftClick = v));
        right.Children.Add(KeyRow("Right click", () => m.RightClick, v => m.RightClick = v));
        right.Children.Add(KeyRow("Middle click", () => m.MiddleClick, v => m.MiddleClick = v));
        right.Children.Add(SectionLabel("Scroll"));
        right.Children.Add(KeyRow("Scroll up", () => m.ScrollUp, v => m.ScrollUp = v));
        right.Children.Add(KeyRow("Scroll down", () => m.ScrollDown, v => m.ScrollDown = v));
        right.Children.Add(KeyRow("Scroll left", () => m.ScrollLeft, v => m.ScrollLeft = v));
        right.Children.Add(KeyRow("Scroll right", () => m.ScrollRight, v => m.ScrollRight = v));

        var binds = Cols(Star, new GridLength(36), Star);
        Place(binds, left, 0);
        Place(binds, right, 2);
        page.Children.Add(Card("Keybinds", "Click a key to change it, then press the new key. Right-click to clear it. Esc cancels.", binds));

        // Movement
        var speed = new StackPanel();
        speed.Children.Add(SliderRow("Start speed", "How fast it moves at first", 50, 1000, 10, () => m.StartSpeed, v => m.StartSpeed = v, v => $"{v:0} px/s"));
        speed.Children.Add(SliderRow("Top speed", "Speed after holding a direction", 200, 4000, 50, () => m.MaxSpeed, v => m.MaxSpeed = v, v => $"{v:0} px/s"));
        speed.Children.Add(SliderRow("Acceleration", "Time to reach top speed", 0, 3, 0.1, () => m.AccelTime, v => m.AccelTime = v, v => $"{v:0.0} s"));
        speed.Children.Add(SliderRow("Boost", "Speed while holding Boost", 1, 6, 0.25, () => m.BoostMultiplier, v => m.BoostMultiplier = v, v => $"×{v:0.##}"));
        speed.Children.Add(SliderRow("Precision", "Speed while holding Precision", 0.05, 1, 0.05, () => m.PrecisionMultiplier, v => m.PrecisionMultiplier = v, v => $"×{v:0.##}"));
        speed.Children.Add(SliderRow("Scroll speed", "Wheel notches per second while held", 1, 40, 1, () => m.ScrollSpeed, v => m.ScrollSpeed = v, v => $"{v:0} /s"));
        page.Children.Add(Card("Movement", "Tune how the virtual mouse feels.", speed));

        // Options
        var options = new StackPanel();
        options.Children.Add(Switch("Show a little \"Mouse mode on\" badge at the bottom of the screen", () => m.ShowIndicator, v => m.ShowIndicator = v));
        var block = Switch("Stop the bound keys from also typing in apps while mouse mode is on", () => m.BlockKeys, v => m.BlockKeys = v);
        block.Margin = new Thickness(0, 12, 0, 0);
        options.Children.Add(block);
        page.Children.Add(Card("Options", null, options));

        return page;
    }

    // ---------- Hotkeys ----------

    FrameworkElement BuildHotkeysPage()
    {
        var h = S.Hotkeys;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Hotkeys",
            "Make your own shortcuts that work anywhere in Windows: type text, press other keys, open apps or websites, control music and more.",
            Switch("Enabled", () => h.Enabled, v => h.Enabled = v)));

        var count = Label("", 13, muted: true);
        _refreshers.Add(() => count.Text = h.Items.Count switch
        {
            0 => "No hotkeys yet",
            1 => "1 hotkey",
            var n => $"{n} hotkeys",
        });
        var add = new Button { Style = St("AccentButton"), Content = "+  New hotkey" };
        add.Click += (_, _) =>
        {
            h.Items.Add(new HotkeyEntry());
            RebuildHotkeys();
            Changed();
            PageScroller.ScrollToEnd();
        };
        var bar = Cols(Star, GridLength.Auto);
        bar.Margin = new Thickness(0, 0, 0, 14);
        Place(bar, count, 0);
        Place(bar, add, 1);
        page.Children.Add(bar);

        page.Children.Add(_hotkeyList);
        RebuildHotkeys();
        return page;
    }

    void RebuildHotkeys()
    {
        _hotkeyList.Children.Clear();
        if (S.Hotkeys.Items.Count == 0)
        {
            _hotkeyList.Children.Add(Card(null, null, Label("Nothing here yet. Hit \"New hotkey\" to make your first one.", muted: true)));
            return;
        }
        foreach (var entry in S.Hotkeys.Items) _hotkeyList.Children.Add(BuildHotkeyCard(entry));
    }

    FrameworkElement BuildHotkeyCard(HotkeyEntry h)
    {
        var enabled = new CheckBox { Style = St("Switch"), IsChecked = h.Enabled, ToolTip = "On / off" };
        RoutedEventHandler onToggle = (_, _) => { h.Enabled = enabled.IsChecked == true; Changed(); };
        enabled.Checked += onToggle;
        enabled.Unchecked += onToggle;

        var name = new TextBox { Text = h.Name, FontSize = 14, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Name" };
        name.TextChanged += (_, _) => { h.Name = name.Text; _e.SaveSoon(); };

        var trigger = KeyBindButton(() => h.Trigger, v => h.Trigger = v, combo: true);
        trigger.MinWidth = 180;
        trigger.ToolTip = "The keys that run this hotkey. Click, then press a combo like Ctrl + Alt + K. Right-click to clear.";

        var delete = new Button { Style = St("GhostButton"), Content = IconGlyph(""), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Delete this hotkey" };
        delete.Click += (_, _) =>
        {
            S.Hotkeys.Items.Remove(h);
            RebuildHotkeys();
            Changed();
        };

        var top = Cols(GridLength.Auto, Star, GridLength.Auto, GridLength.Auto);
        Place(top, enabled, 0);
        Place(top, name, 1);
        Place(top, trigger, 2);
        Place(top, delete, 3);

        var editor = new ContentControl { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Content = BuildActionEditor(h) };
        var action = new ComboBox { Width = 240, ItemsSource = ActionLabels, SelectedIndex = (int)h.Action, VerticalAlignment = VerticalAlignment.Top };
        action.SelectionChanged += (_, _) =>
        {
            h.Action = (HotkeyActionType)action.SelectedIndex;
            editor.Content = BuildActionEditor(h);
            Changed();
        };

        var does = Label("Does", 13, muted: true);
        does.Width = 56;
        does.VerticalAlignment = VerticalAlignment.Top;
        does.Margin = new Thickness(0, 7, 0, 0);
        var bottom = Cols(GridLength.Auto, GridLength.Auto, Star);
        bottom.Margin = new Thickness(0, 14, 0, 0);
        Place(bottom, does, 0);
        Place(bottom, action, 1);
        Place(bottom, editor, 2);

        var body = new StackPanel();
        body.Children.Add(top);
        body.Children.Add(bottom);
        return new Border { Style = St("Card"), Child = body };
    }

    FrameworkElement BuildActionEditor(HotkeyEntry h)
    {
        switch (h.Action)
        {
            case HotkeyActionType.TypeText:
            {
                var box = new TextBox
                {
                    Text = h.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 34, MaxHeight = 160,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = "The text to type. Press Enter for a new line.",
                };
                box.TextChanged += (_, _) => { h.Text = box.Text; _e.SaveSoon(); };
                return box;
            }
            case HotkeyActionType.SendKeys:
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(KeyBindButton(() => h.Keys, v => h.Keys = v, combo: true));
                var note = Label("the keys to press instead", 12, muted: true);
                note.Margin = new Thickness(12, 0, 0, 0);
                row.Children.Add(note);
                return row;
            }
            case HotkeyActionType.Launch:
            {
                var target = new TextBox { Text = h.Target, VerticalAlignment = VerticalAlignment.Center, ToolTip = "An app (.exe), file, folder or website like https://youtube.com" };
                target.TextChanged += (_, _) => { h.Target = target.Text; _e.SaveSoon(); };
                var browse = new Button { Style = St("GhostButton"), Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
                browse.Click += (_, _) =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Pick an app or file to open", Filter = "Apps|*.exe;*.lnk;*.bat;*.cmd|All files|*.*" };
                    if (dialog.ShowDialog(this) == true) target.Text = dialog.FileName;
                };
                var line = Cols(Star, GridLength.Auto);
                Place(line, target, 0);
                Place(line, browse, 1);

                var args = new TextBox { Text = h.Arguments, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Optional extra arguments" };
                args.TextChanged += (_, _) => { h.Arguments = args.Text; _e.SaveSoon(); };
                var argsLabel = Label("Arguments", 12, muted: true);
                argsLabel.Width = 80;
                var argsLine = Cols(GridLength.Auto, Star);
                argsLine.Margin = new Thickness(0, 8, 0, 0);
                Place(argsLine, argsLabel, 0);
                Place(argsLine, args, 1);

                var help = Label("An app (.exe), file, folder or website like https://youtube.com", 11.5, muted: true);
                help.Margin = new Thickness(0, 6, 0, 0);

                var stack = new StackPanel();
                stack.Children.Add(line);
                stack.Children.Add(help);
                stack.Children.Add(argsLine);
                return stack;
            }
            case HotkeyActionType.Media:
            {
                var box = new ComboBox
                {
                    Width = 200, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = (int)h.Media,
                    ItemsSource = new[] { "Play / pause", "Next track", "Previous track", "Volume up", "Volume down", "Mute" },
                };
                box.SelectionChanged += (_, _) => { h.Media = (MediaAction)box.SelectedIndex; _e.SaveSoon(); };
                return box;
            }
            case HotkeyActionType.MouseClick:
            {
                var box = new ComboBox
                {
                    Width = 200, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = (int)h.Button,
                    ItemsSource = new[] { "Left click", "Right click", "Middle click" },
                };
                box.SelectionChanged += (_, _) => { h.Button = (ClickButton)box.SelectedIndex; _e.SaveSoon(); };
                return box;
            }
            case HotkeyActionType.ToggleMouseMode:
                return Label("Turns Keyboard Mouse mode on or off.", 12.5, muted: true);
            default:
                return Label("Pins or unpins the Potato Keys+ window on top of everything.", 12.5, muted: true);
        }
    }

    // ---------- Ideas ----------

    FrameworkElement BuildIdeasPage()
    {
        var page = new StackPanel();
        page.Children.Add(PageHeader("More modules",
            "Ideas for what Potato Keys+ can do next. These aren't built yet. Pick your favourites and they can become real modules.", null));

        (string Name, string Text)[] ideas =
        [
            ("Grid Jump", "Splits the screen into a lettered grid. Type two letters and the mouse jumps right there. Way faster than arrows."),
            ("Key Remapper", "Swap or disable any key. Turn Caps Lock into Esc, or fix a broken key by using another one."),
            ("Text Expander", "Type a short code like ;mail and it turns into your full email or a whole message."),
            ("Macro Recorder", "Record keys and clicks once, then replay the whole thing with one hotkey."),
            ("Auto Clicker", "Keyboard-toggled clicker with a clicks-per-second slider."),
            ("Keystroke Overlay", "Shows the keys you press on screen. Great for tutorials and YouTube recordings."),
            ("Window Snapper", "Move, resize and snap windows to halves and corners with keys."),
            ("Layers", "Hold a key like Caps Lock to turn IJKL into arrows, numbers into F-keys and more."),
            ("Clipboard History", "Keeps the last things you copied. Paste any of them with a hotkey."),
            ("App Profiles", "Different keybinds per app, like one setup for Minecraft and another for Chrome."),
            ("Key Stats", "Counts your key presses and shows a heatmap of your keyboard."),
            ("On-screen Keyboard", "A pinnable keyboard you can type on with the virtual mouse."),
            ("Gamer Mode", "One switch that blocks the Win key, Alt+Tab and sticky-keys popups while you play."),
            ("Quick Launcher", "Press a hotkey, type a few letters, hit Enter: open any app, site or file."),
            ("Turbo Keys", "Hold a key and it repeats as fast as you want, like a turbo button."),
            ("Panic Key", "One key hides every window, mutes the sound and pauses media."),
            ("Key Sounds", "Satisfying mechanical keyboard click sounds while you type."),
            ("Typing Trainer", "Mini typing games and a WPM tracker that learns from your real typing."),
            ("Snippet Wheel", "Hold a key to pop up a radial menu of your favourite texts and actions."),
            ("Mouse Jiggler", "Keeps your PC awake and your status green by nudging the mouse."),
            ("Scroll Lock Mode", "Use Scroll Lock as a switch that turns the whole keyboard into a different layout."),
            ("Screen Ruler", "Measure pixels and pick colours anywhere on screen, all from the keyboard."),
        ];

        var wrap = new WrapPanel();
        foreach (var (name, text) in ideas)
        {
            var body = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Label(name, 14.5, bold: true));
            var soon = new Border
            {
                Background = Res("AccentDimBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1, 7, 2),
                Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "idea", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Res("AccentBrush") },
            };
            head.Children.Add(soon);
            body.Children.Add(head);
            var desc = Label(text, 12.5, muted: true);
            desc.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(desc);

            wrap.Children.Add(new Border { Style = St("Card"), Width = 270, MinHeight = 112, Margin = new Thickness(0, 0, 14, 14), Child = body });
        }
        page.Children.Add(wrap);
        return page;
    }

    // ---------- Building blocks ----------

    Brush Res(string key) => (Brush)FindResource(key);

    Style St(string key) => (Style)FindResource(key);

    static Grid Cols(params GridLength[] widths)
    {
        var grid = new Grid();
        foreach (var w in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        return grid;
    }

    static void Place(Grid grid, UIElement child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    TextBlock Label(string text, double size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = Res(muted ? "MutedBrush" : "TextBrush"),
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    TextBlock IconGlyph(string glyph) => new() { Text = glyph, FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 14 };

    TextBlock SectionLabel(string text, bool first = false) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = Res("AccentBrush"),
        Margin = new Thickness(0, first ? 0 : 18, 0, 4),
    };

    Border Card(string? title, string? subtitle, UIElement content)
    {
        var stack = new StackPanel();
        if (title != null) stack.Children.Add(Label(title, 16, bold: true));
        if (subtitle != null)
        {
            var sub = Label(subtitle, 12.5, muted: true);
            sub.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(sub);
        }
        if ((title != null || subtitle != null) && content is FrameworkElement fe) fe.Margin = new Thickness(0, 16, 0, 0);
        stack.Children.Add(content);
        return new Border { Style = St("Card"), Child = stack };
    }

    FrameworkElement PageHeader(string title, string description, CheckBox? enabledSwitch)
    {
        var grid = Cols(Star, GridLength.Auto);
        grid.Margin = new Thickness(0, 0, 0, 20);
        var text = new StackPanel();
        text.Children.Add(Label(title, 26, bold: true));
        var desc = Label(description, 13, muted: true);
        desc.Margin = new Thickness(0, 4, 24, 0);
        text.Children.Add(desc);
        Place(grid, text, 0);
        if (enabledSwitch != null)
        {
            enabledSwitch.VerticalAlignment = VerticalAlignment.Top;
            enabledSwitch.Margin = new Thickness(0, 10, 0, 0);
            Place(grid, enabledSwitch, 1);
        }
        return grid;
    }

    CheckBox Switch(string? text, Func<bool> get, Action<bool> set)
    {
        var box = new CheckBox { Style = St("Switch"), Content = text, IsChecked = get() };
        RoutedEventHandler handler = (_, _) =>
        {
            if (_syncing) return;
            set(box.IsChecked == true);
            Changed();
        };
        box.Checked += handler;
        box.Unchecked += handler;
        _switches.Add((box, get));
        return box;
    }

    Button KeyBindButton(Func<KeyBind> get, Action<KeyBind> set, bool combo)
    {
        var button = new Button
        {
            Style = St("KeyBindButton"),
            Content = get().ToString(),
            ToolTip = combo ? "Click, then press a key or combo. Right-click to clear." : "Click, then press a key. Right-click to clear.",
        };
        button.Click += (_, _) =>
        {
            button.Content = combo ? "Press a combo…" : "Press a key…";
            button.Tag = "capturing";
            _e.Hook.BeginCapture(combo, bind =>
            {
                button.Tag = null;
                if (bind != null)
                {
                    set(bind);
                    Changed();
                }
                button.Content = get().ToString();
            });
        };
        button.MouseRightButtonUp += (_, args) =>
        {
            set(new KeyBind());
            button.Content = get().ToString();
            Changed();
            args.Handled = true;
        };
        return button;
    }

    FrameworkElement KeyRow(string label, Func<KeyBind> get, Action<KeyBind> set, bool combo = false)
    {
        var row = Cols(Star, GridLength.Auto);
        row.Margin = new Thickness(0, 4, 0, 4);
        Place(row, Label(label), 0);
        Place(row, KeyBindButton(get, set, combo), 1);
        return row;
    }

    FrameworkElement SliderRow(string label, string help, double min, double max, double step,
        Func<double> get, Action<double> set, Func<double, string> format)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label(label));
        text.Children.Add(Label(help, 11.5, muted: true));

        var value = new TextBlock
        {
            Text = format(get()), FontFamily = (FontFamily)FindResource("KeyFont"), FontSize = 12.5,
            Foreground = Res("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
        };
        var slider = new Slider
        {
            Minimum = min, Maximum = max, Value = get(), TickFrequency = step, IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 16, 0),
        };
        slider.ValueChanged += (_, e) =>
        {
            set(e.NewValue);
            value.Text = format(e.NewValue);
            _e.SaveSoon();
        };

        var row = Cols(new GridLength(220), Star, new GridLength(84));
        row.Margin = new Thickness(0, 7, 0, 7);
        Place(row, text, 0);
        Place(row, slider, 1);
        Place(row, value, 2);
        return row;
    }
}
