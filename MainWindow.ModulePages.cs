using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PotatoKeysPlus.Audio;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

// Pages for Quick Launcher, Snippet Wheel, Gamer Mode, Panic Key, Key Sounds, Typing Trainer and Mouse Jiggler.
public partial class MainWindow
{
    // ---------- Shared: live status card with an on/off button ----------

    Border StatusCard(Func<bool> isOn, Func<string> title, Func<string> hint, Action toggle, Func<bool> enabled,
        string onButton = "Turn off", string offButton = "Turn on")
    {
        var dot = new Ellipse { Width = 12, Height = 12, Margin = new Thickness(2, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        var titleText = Label("", 17, bold: true);
        var hintText = Label("", 13, muted: true);
        hintText.Margin = new Thickness(0, 3, 16, 0);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(titleText);
        text.Children.Add(hintText);
        var button = new Button { Style = St("AccentButton"), MinWidth = 120, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => toggle();

        var grid = Cols(GridLength.Auto, Star, GridLength.Auto);
        Place(grid, dot, 0);
        Place(grid, text, 1);
        Place(grid, button, 2);
        var card = new Border { Style = St("Card"), Child = grid };

        _refreshers.Add(() =>
        {
            bool on = isOn();
            titleText.Text = title();
            hintText.Text = hint();
            button.Content = on ? onButton : offButton;
            button.IsEnabled = enabled();
            dot.Fill = Res(on ? "AccentBrush" : enabled() ? "GoodBrush" : "RaisedBrush");
            card.BorderBrush = Res(on ? "AccentBrush" : "CardBorderBrush");
        });
        return card;
    }

    TextBox SettingBox(string text, Action<string> set, string? tooltip = null)
    {
        var box = new TextBox { Text = text, ToolTip = tooltip };
        box.TextChanged += (_, _) => { set(box.Text); _e.SaveSoon(); };
        return box;
    }

    StackPanel Stack(params UIElement[] children)
    {
        var stack = new StackPanel();
        for (int i = 0; i < children.Length; i++)
        {
            if (i > 0 && children[i] is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(0, 12, 0, 0);
            stack.Children.Add(children[i]);
        }
        return stack;
    }

    // ---------- Quick Launcher ----------

    FrameworkElement BuildLauncherPage()
    {
        var l = S.Launcher;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Quick Launcher",
            "Press a hotkey, type a few letters, hit Enter. Opens any app, website, file or Potato Keys+ command, does quick maths, and searches the web.",
            Switch("Enabled", () => l.Enabled, v => l.Enabled = v)));

        var open = new Button { Style = St("AccentButton"), Content = "Open it now", HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) => _launcherWindow.Open();
        page.Children.Add(Card("Hotkey", "Alt + Space by default. Change it if another app already uses it.",
            Stack(KeyRow("Open Quick Launcher", () => l.Hotkey, v => l.Hotkey = v, combo: true), open)));

        var url = SettingBox(l.SearchUrl, v => l.SearchUrl = v, "{0} is replaced with what you typed");
        var urlRow = Cols(new GridLength(150), Star);
        Place(urlRow, Label("Search website", 13, muted: true), 0);
        Place(urlRow, url, 1);
        var reset = new Button { Style = St("GhostButton"), Content = "Forget my favourites", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) => { l.Usage.Clear(); _e.SaveSoon(); };

        page.Children.Add(Card("What it can do", null, Stack(
            Label("• Apps: every app in your Start menu, including Store apps. The ones you open most go to the top.\n" +
                  "• Maths: type =12*7 or (5+3)/2 and press Enter to copy the answer.\n" +
                  "• Websites: type youtube.com to open it.\n" +
                  "• Files and folders: type or paste a path like C:\\Users.\n" +
                  "• Commands: Mouse mode, Gamer Mode, Mouse Jiggler, Key Sounds, Typing test, Lock PC.", 13, muted: true),
            Switch("Quick maths", () => l.Calculator, v => l.Calculator = v),
            Switch("Offer a web search when nothing matches", () => l.WebSearch, v => l.WebSearch = v),
            urlRow,
            reset)));
        return page;
    }

    // ---------- Snippet Wheel ----------

    readonly StackPanel _snippetList = new();

    FrameworkElement BuildSnippetsPage()
    {
        var s = S.Snippets;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Snippet Wheel",
            "Hold the trigger and a round menu pops up at your mouse. Pick with 1-8, the arrows or the mouse, then let go. Great for text you type all the time and shortcuts you can never remember.",
            Switch("Enabled", () => s.Enabled, v => s.Enabled = v)));

        var preview = new Button { Style = St("AccentButton"), Content = "Show the wheel", HorizontalAlignment = HorizontalAlignment.Left };
        preview.Click += (_, _) => _e.Snippets.OpenSticky();
        page.Children.Add(Card("Trigger",
            "Hold it, pick, release. A quick tap keeps the wheel open until you pick something or press Esc.",
            Stack(KeyRow("Hold to open the wheel", () => s.Trigger, v => s.Trigger = v, combo: true), preview)));

        var add = new Button { Style = St("AccentButton"), Content = "+  Add item" };
        add.Click += (_, _) =>
        {
            if (s.Items.Count >= SnippetWheelModule.MaxItems) return;
            s.Items.Add(new HotkeyEntry { Name = "New item" });
            RebuildSnippets();
            Changed();
        };
        var count = Label("", 13, muted: true);
        _refreshers.Add(() =>
        {
            count.Text = $"{s.Items.Count} of {SnippetWheelModule.MaxItems} slots used";
            add.IsEnabled = s.Items.Count < SnippetWheelModule.MaxItems;
        });
        var bar = Cols(Star, GridLength.Auto);
        bar.Margin = new Thickness(0, 4, 0, 14);
        Place(bar, count, 0);
        Place(bar, add, 1);
        page.Children.Add(bar);
        page.Children.Add(_snippetList);
        RebuildSnippets();
        return page;
    }

    void RebuildSnippets()
    {
        _snippetList.Children.Clear();
        var items = S.Snippets.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var h = items[i];
            int index = i;

            var number = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Res("AccentDimBrush"),
                Child = new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = Res("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var name = new TextBox { Text = h.Name, FontSize = 14, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Label shown on the wheel" };
            name.TextChanged += (_, _) => { h.Name = name.Text; _e.SaveSoon(); };

            Button Small(string glyph, string tip, Action click)
            {
                var b = new Button { Style = St("GhostButton"), Content = IconGlyph(glyph), Margin = new Thickness(6, 0, 0, 0), ToolTip = tip, Padding = new Thickness(10, 7, 10, 7) };
                b.Click += (_, _) => { click(); RebuildSnippets(); Changed(); };
                return b;
            }
            var up = Small("\uE70E", "Move up", () => { if (index > 0) (items[index - 1], items[index]) = (items[index], items[index - 1]); });
            var down = Small("\uE70D", "Move down", () => { if (index < items.Count - 1) (items[index + 1], items[index]) = (items[index], items[index + 1]); });
            var del = Small("\uE74D", "Remove", () => items.RemoveAt(index));

            var top = Cols(GridLength.Auto, Star, GridLength.Auto, GridLength.Auto, GridLength.Auto);
            Place(top, number, 0);
            Place(top, name, 1);
            Place(top, up, 2);
            Place(top, down, 3);
            Place(top, del, 4);

            var editor = new ContentControl { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Content = BuildActionEditor(h) };
            // The wheel can't toggle itself, so offer every action except the window ones.
            var action = new ComboBox { Width = 240, ItemsSource = ActionLabels, SelectedIndex = (int)h.Action, VerticalAlignment = VerticalAlignment.Top };
            action.SelectionChanged += (_, _) =>
            {
                h.Action = (HotkeyActionType)action.SelectedIndex;
                editor.Content = BuildActionEditor(h);
                Changed();
            };
            var does = Label("Does", 13, muted: true);
            does.Width = 56;
            does.Margin = new Thickness(0, 7, 0, 0);
            does.VerticalAlignment = VerticalAlignment.Top;
            var bottom = Cols(GridLength.Auto, GridLength.Auto, Star);
            bottom.Margin = new Thickness(0, 14, 0, 0);
            Place(bottom, does, 0);
            Place(bottom, action, 1);
            Place(bottom, editor, 2);

            var body = new StackPanel();
            body.Children.Add(top);
            body.Children.Add(bottom);
            _snippetList.Children.Add(new Border { Style = St("Card"), Child = body });
        }
    }

    // ---------- Gamer Mode ----------

    FrameworkElement BuildGamerPage()
    {
        var g = S.Gamer;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Gamer Mode",
            "Stops the keys that throw you out of a game: the Windows key, Alt+Tab, and the Sticky Keys popup. It can switch on by itself when a game is open.",
            Switch("Enabled", () => g.Enabled, v => { g.Enabled = v; _e.Gamer.Apply(); })));

        page.Children.Add(StatusCard(
            () => _e.Gamer.Active,
            () => !g.Enabled ? "Gamer Mode is turned off" : _e.Gamer.Active ? "Gamer Mode is ON" : "Gamer Mode is off",
            () => !g.Enabled ? "Flip the Enabled switch to use it."
                : _e.Gamer.AutoApp is { } app ? $"Switched on automatically for {app}. Press {g.Toggle} to turn it off."
                : _e.Gamer.Active ? $"Blocking keys now. Press {g.Toggle} to stop." : $"Press {g.Toggle} in a game, or let it switch on by itself.",
            () => _e.Gamer.SetActive(!_e.Gamer.Active),
            () => g.Enabled));

        page.Children.Add(Card("Hotkey", null, KeyRow("Turn Gamer Mode on / off", () => g.Toggle, v => g.Toggle = v, combo: true)));

        page.Children.Add(Card("While it's on, block", null, Stack(
            Switch("Windows key (and Ctrl + Esc)", () => g.BlockWinKey, v => g.BlockWinKey = v),
            Switch("Alt + Tab", () => g.BlockAltTab, v => g.BlockAltTab = v),
            Switch("Alt + F4 (so you can't close the game by accident)", () => g.BlockAltF4, v => g.BlockAltF4 = v),
            Switch("Sticky Keys / Filter Keys popups (pressing Shift 5 times, holding Shift)", () => g.BlockAccessibilityPopups, v => { g.BlockAccessibilityPopups = v; _e.Gamer.Apply(); }))));

        var apps = new TextBox { Text = g.Apps, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 34, ToolTip = "Program names, separated by commas" };
        apps.TextChanged += (_, _) => { g.Apps = apps.Text; _e.SaveSoon(); };
        var pick = new Button { Style = St("GhostButton"), Content = "Add a running app…", HorizontalAlignment = HorizontalAlignment.Left };
        pick.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            var running = Process.GetProcesses()
                .Where(p => { try { return p.MainWindowHandle != IntPtr.Zero && p.Id != Environment.ProcessId; } catch { return false; } })
                .Select(p => (Exe: p.ProcessName + ".exe", Title: p.MainWindowTitle))
                .DistinctBy(p => p.Exe).OrderBy(p => p.Exe, StringComparer.OrdinalIgnoreCase);
            foreach (var (exe, title) in running)
            {
                var item = new MenuItem { Header = string.IsNullOrWhiteSpace(title) ? exe : $"{exe}   ({(title.Length > 40 ? title[..40] + "…" : title)})" };
                item.Click += (_, _) =>
                {
                    if (!apps.Text.Contains(exe, StringComparison.OrdinalIgnoreCase))
                        apps.Text = string.IsNullOrWhiteSpace(apps.Text) ? exe : apps.Text.TrimEnd(' ', ',') + ", " + exe;
                };
                menu.Items.Add(item);
            }
            pick.ContextMenu = menu;
            menu.PlacementTarget = pick;
            menu.IsOpen = true;
        };
        page.Children.Add(Card("Turn on by itself", "When one of these programs is the window in front, Gamer Mode switches on, and it switches off again when you leave.", Stack(
            Switch("Turn on automatically for these games", () => g.AutoForApps, v => g.AutoForApps = v),
            apps,
            pick,
            Label("Tip: Java Minecraft is javaw.exe, Bedrock is Minecraft.Windows.exe, Roblox is RobloxPlayerBeta.exe.", 12, muted: true))));
        return page;
    }

    // ---------- Panic Key ----------

    FrameworkElement BuildPanicPage()
    {
        var p = S.Panic;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Panic Key",
            "One combo and your screen is clear: every window minimized, sound muted, music and videos paused. Press it again and everything comes back.",
            Switch("Enabled", () => p.Enabled, v => p.Enabled = v)));

        page.Children.Add(StatusCard(
            () => _e.Panic.Panicked,
            () => _e.Panic.Panicked ? "Panic mode is on" : "Ready",
            () => _e.Panic.Panicked ? $"Press {p.Hotkey} again to bring everything back." : $"Press {p.Hotkey} anywhere.",
            () => _ = _e.Panic.TriggerAsync(),
            () => p.Enabled,
            onButton: "Restore", offButton: "Test it"));

        page.Children.Add(Card("Hotkey", null, KeyRow("Panic!", () => p.Hotkey, v => p.Hotkey = v, combo: true)));

        var open = SettingBox(p.OpenTarget, v => p.OpenTarget = v, "Optional: an app, file or website to open, e.g. a homework doc");
        var openRow = Cols(new GridLength(200), Star);
        Place(openRow, Label("Also open (optional)", 13, muted: true), 0);
        Place(openRow, open, 1);

        page.Children.Add(Card("What happens", null, Stack(
            Switch("Minimize every window", () => p.MinimizeAll, v => p.MinimizeAll = v),
            Switch("Mute the sound", () => p.Mute, v => p.Mute = v),
            Switch("Pause music and videos (Spotify, YouTube, …)", () => p.PauseMedia, v => p.PauseMedia = v),
            Switch("Lock the PC", () => p.LockPc, v => p.LockPc = v),
            openRow,
            Switch("Press again to restore everything", () => p.PressAgainToRestore, v => p.PressAgainToRestore = v))));
        return page;
    }

    // ---------- Key Sounds ----------

    FrameworkElement BuildSoundsPage()
    {
        var k = S.Sounds;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Key Sounds",
            "Satisfying mechanical keyboard sounds for every key you press, on any keyboard. All sounds are made by Potato Keys+ itself.",
            Switch("Enabled", () => k.Enabled, v => { k.Enabled = v; _e.Sounds.Apply(); })));

        var packs = new WrapPanel();
        var packButtons = new List<(Button Button, string Pack)>();
        string[] about = ["Deep, creamy", "Sharp blue switch", "Old-school + bell", "Poppy and cute", "Quiet laptop"];
        for (int i = 0; i < SoundSynth.Packs.Length; i++)
        {
            var name = SoundSynth.Packs[i];
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            content.Children.Add(new TextBlock { Text = about[i], FontSize = 11.5, Opacity = 0.75 });
            var b = new Button { Content = content, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(16, 10, 16, 10), MinWidth = 140 };
            b.Click += async (_, _) =>
            {
                k.Pack = name;
                _e.Sounds.Apply();
                Changed();
                await _e.Sounds.PreviewAsync();
            };
            packButtons.Add((b, name));
            packs.Children.Add(b);
        }
        _refreshers.Add(() =>
        {
            foreach (var (b, name) in packButtons) b.Style = St(name == k.Pack ? "AccentButton" : "GhostButton");
        });

        var preview = new Button { Style = St("GhostButton"), Content = "Play preview", HorizontalAlignment = HorizontalAlignment.Left };
        preview.Click += async (_, _) => await _e.Sounds.PreviewAsync();
        page.Children.Add(Card("Sound pack", "Click one to hear it.", Stack(packs, preview)));

        var error = Label("", 12.5);
        error.Foreground = Res("DangerBrush");
        _refreshers.Add(() =>
        {
            error.Text = _e.Sounds.LastError ?? "";
            error.Visibility = _e.Sounds.LastError == null ? Visibility.Collapsed : Visibility.Visible;
        });
        page.Children.Add(Card("Options", null, Stack(
            SliderRow("Volume", "How loud the clicks are", 0.05, 1, 0.05, () => k.Volume, v => k.Volume = v, v => $"{v * 100:0}%"),
            Switch("Also play a soft sound when keys are released", () => k.KeyUpSounds, v => k.KeyUpSounds = v),
            Switch("Stay quiet while Gamer Mode is on", () => k.MuteInGamerMode, v => k.MuteInGamerMode = v),
            error)));
        return page;
    }

    // ---------- Typing Trainer ----------

    FrameworkElement BuildTypingPage()
    {
        var t = S.Typing;
        var trainer = _e.Typing;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Typing Trainer",
            "See how fast you really type while you use your PC, and get faster with tests that focus on the letters you miss most.",
            Switch("Track my typing speed", () => t.Enabled, v => t.Enabled = v)));

        // Live numbers
        TextBlock Big() => new() { FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Res("AccentBrush") };
        FrameworkElement Stat(TextBlock value, string label)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 34, 0) };
            s.Children.Add(value);
            s.Children.Add(Label(label, 12, muted: true));
            return s;
        }
        var live = Big(); var words = Big(); var avg = Big(); var best = Big();
        var stats = new WrapPanel();
        stats.Children.Add(Stat(live, "WPM right now"));
        stats.Children.Add(Stat(words, "words today"));
        stats.Children.Add(Stat(avg, "average WPM today"));
        stats.Children.Add(Stat(best, "best burst ever"));
        var privacy = Label("Only key counts are saved, never what you type.", 12, muted: true);

        // Last 7 days
        var chart = new Grid { Height = 120, Margin = new Thickness(0, 16, 0, 0) };
        for (int i = 0; i < 7; i++) chart.ColumnDefinitions.Add(new ColumnDefinition());

        void UpdateNumbers()
        {
            var today = trainer.Today();
            live.Text = $"{trainer.LiveWpm:0}";
            words.Text = $"{today.Chars / 5:N0}";
            avg.Text = $"{TypingTrainerModule.AverageWpm(today):0}";
            best.Text = $"{t.BestBurstWpm:0}";
        }

        void UpdateChart()
        {
            chart.Children.Clear();
            var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToList();
            var values = days.Select(d => t.Days.TryGetValue(d.ToString("yyyy-MM-dd"), out var day) ? day.Chars / 5.0 : 0).ToList();
            double max = Math.Max(values.Max(), 1);
            for (int i = 0; i < 7; i++)
            {
                var col = new Grid { Margin = new Thickness(6, 0, 6, 0) };
                col.RowDefinitions.Add(new RowDefinition());
                col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var bar = new Border
                {
                    VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(5, 5, 2, 2),
                    Height = Math.Max(3, 88 * values[i] / max), Background = Res(i == 6 ? "AccentBrush" : "RaisedBrush"),
                    ToolTip = $"{values[i]:N0} words",
                };
                var day = Label(i == 6 ? "Today" : days[i].ToString("ddd"), 11.5, muted: true);
                day.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetRow(day, 1);
                col.Children.Add(bar);
                col.Children.Add(day);
                Grid.SetColumn(col, i);
                chart.Children.Add(col);
            }
        }

        page.Children.Add(Card("Your typing", null, Stack(stats, privacy)));
        page.Children.Add(Card("Last 7 days", "Words typed per day.", chart));

        // Typing test
        var prompt = new TextBlock { FontSize = 21, TextWrapping = TextWrapping.Wrap, LineHeight = 34, FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI") };
        var input = new TextBox { FontSize = 17, Margin = new Thickness(0, 16, 0, 0), ToolTip = "Type the text above. Tab starts a new test." };
        var result = Label("Start typing to begin. Tab = new test.", 13.5, muted: true);
        var weak = Label("", 12.5, muted: true);
        var newTest = new Button { Style = St("AccentButton"), Content = "New test", HorizontalAlignment = HorizontalAlignment.Left };

        string target = "";
        DateTime? started = null;
        int typedCount = 0, keystrokes = 0, mistakes = 0;
        bool finished = false;

        void Render(string typed)
        {
            prompt.Inlines.Clear();
            for (int i = 0; i < target.Length; i++)
            {
                var run = new Run(target[i].ToString());
                if (i < typed.Length)
                {
                    bool ok = typed[i] == target[i];
                    run.Foreground = Res(ok ? "TextBrush" : "DangerBrush");
                    if (!ok) run.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xE5, 0x53, 0x4B));
                }
                else
                {
                    run.Foreground = Res("MutedBrush");
                    if (i == typed.Length && !finished) run.TextDecorations = TextDecorations.Underline;
                }
                prompt.Inlines.Add(run);
            }
        }

        void ShowWeak()
        {
            var letters = trainer.WeakLetters(5).ToList();
            weak.Text = letters.Count == 0
                ? "Do a few tests and your weakest letters will show up here. New tests use more of them."
                : "Letters you miss most: " + string.Join("   ", letters.Select(l => $"{char.ToUpperInvariant(l.Letter)} ({l.MissRate:P0})"));
        }

        void NewTest()
        {
            target = trainer.MakeTest();
            started = null;
            typedCount = keystrokes = mistakes = 0;
            finished = false;
            input.IsReadOnly = false;
            input.Text = "";
            result.Text = "Start typing to begin. Tab = new test.";
            Render("");
            ShowWeak();
            input.Focus();
        }

        input.TextChanged += (_, _) =>
        {
            if (finished) return;
            var typed = input.Text;
            if (started == null && typed.Length > 0) started = DateTime.Now;
            for (int i = typedCount; i < typed.Length && i < target.Length; i++)
            {
                keystrokes++;
                bool missed = typed[i] != target[i];
                if (missed) mistakes++;
                trainer.RecordLetter(target[i], missed);
            }
            typedCount = typed.Length;
            Render(typed);

            if (typed.Length >= target.Length && started is DateTime start)
            {
                finished = true;
                input.IsReadOnly = true;
                double minutes = Math.Max((DateTime.Now - start).TotalMinutes, 1 / 60.0);
                int correct = target.Where((c, i) => i < typed.Length && typed[i] == c).Count();
                double wpm = correct / 5.0 / minutes;
                double accuracy = keystrokes == 0 ? 0 : 100.0 * (keystrokes - mistakes) / keystrokes;
                if (wpm > 250)
                {
                    // Faster than the world record: text was pasted or typed by a program, so don't count it.
                    result.Text = "That was too fast to be real typing, so it wasn't counted. (Tab for another)";
                    result.Foreground = Res("DangerBrush");
                    Render(typed);
                    return;
                }
                bool record = wpm > t.BestTestWpm;
                trainer.RecordTest(wpm, accuracy);
                result.Text = $"{wpm:0} WPM  ·  {accuracy:0}% accuracy" + (record ? "  ·  new personal best!" : $"  ·  best: {t.BestTestWpm:0} WPM") + "   (Tab for another)";
                result.Foreground = Res("AccentBrush");
                Render(typed);
                ShowWeak();
            }
            else
            {
                result.Foreground = Res("MutedBrush");
            }
        };
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Tab) { NewTest(); e.Handled = true; }
        };
        newTest.Click += (_, _) => NewTest();
        DataObject.AddPastingHandler(input, (_, e) => e.CancelCommand()); // no pasting in a typing test

        page.Children.Add(Card("Typing test", "25 common words, picked to train the letters you miss most.", Stack(prompt, input, result, weak, newTest)));

        NewTest();
        UpdateNumbers();
        UpdateChart();
        // Keep the numbers live while the page is on screen.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            if (!page.IsVisible) return;
            UpdateNumbers();
            if (DateTime.Now.Second % 10 == 0) UpdateChart();
        };
        timer.Start();
        return page;
    }

    // ---------- Mouse Jiggler ----------

    FrameworkElement BuildJigglerPage()
    {
        var j = S.Jiggler;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Mouse Jiggler",
            "Keeps your PC awake and your Teams / Discord / Slack status active. The mouse moves one pixel and straight back, so you won't see it.",
            Switch("Enabled", () => j.Enabled, v => { j.Enabled = v; if (!v) _e.Jiggler.SetRunning(false); })));

        page.Children.Add(StatusCard(
            () => _e.Jiggler.Running,
            () => _e.Jiggler.Running ? "Jiggling" : "Not running",
            () => _e.Jiggler.Running
                ? $"Running for {(int)_e.Jiggler.RunningFor.TotalMinutes} min · nudged {_e.Jiggler.JiggleCount} times. Press {j.Toggle} to stop."
                : $"Press {j.Toggle} or click Start.",
            () => _e.Jiggler.SetRunning(!_e.Jiggler.Running),
            () => j.Enabled,
            onButton: "Stop", offButton: "Start"));

        // The "running for" text needs ticking while it runs.
        var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        tick.Tick += (_, _) => { if (page.IsVisible && _e.Jiggler.Running) RefreshStatus(); };
        tick.Start();

        page.Children.Add(Card("Hotkey", null, KeyRow("Start / stop", () => j.Toggle, v => j.Toggle = v, combo: true)));
        page.Children.Add(Card("Options", null, Stack(
            SliderRow("Nudge every", "How often the mouse moves", 10, 300, 5, () => j.IntervalSeconds, v => j.IntervalSeconds = v, v => $"{v:0} s"),
            SliderRow("Stop after", "Turns itself off (0 = never)", 0, 480, 15, () => j.StopAfterMinutes, v => j.StopAfterMinutes = v, v => v == 0 ? "never" : $"{v:0} min"),
            Switch("Only nudge when I'm not using the PC", () => j.OnlyWhenIdle, v => j.OnlyWhenIdle = v),
            Switch("Keep the screen on too", () => j.KeepScreenOn, v => { j.KeepScreenOn = v; _e.Jiggler.ApplyKeepAwake(); }))));
        return page;
    }
}
