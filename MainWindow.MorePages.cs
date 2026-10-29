using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;
using PotatoKeysPlus.Services;

namespace PotatoKeysPlus;

// Pages for App Volume, Pin Any Window, Text Tools, Text Expander, Window Snapper, Grid Jump,
// Symbol Lock, Screenshot and Quick Notes, plus the list used to warn about clashing hotkeys.
public partial class MainWindow
{
    /// <summary>Every key bind in the app with a friendly name, for clash warnings.</summary>
    IEnumerable<(string Name, KeyBind Bind)> AllKeyBinds()
    {
        yield return ("Safety Lock", S.SafetyKey);
        yield return ("Keyboard Mouse: turn on / off", S.Mouse.Toggle);
        foreach (var h in S.Hotkeys.Items) yield return ($"Hotkey \"{h.Name}\"", h.Trigger);
        yield return ("Quick Launcher", S.Launcher.Hotkey);
        yield return ("Snippet Wheel", S.Snippets.Trigger);
        yield return ("Gamer Mode", S.Gamer.Toggle);
        yield return ("Panic Key", S.Panic.Hotkey);
        yield return ("Mouse Jiggler", S.Jiggler.Toggle);
        yield return ("App Volume: up", S.AppVolume.Up);
        yield return ("App Volume: down", S.AppVolume.Down);
        yield return ("App Volume: mute", S.AppVolume.Mute);
        yield return ("Pin Any Window", S.WindowPin.Toggle);
        yield return ("Pin Any Window: more see-through", S.WindowPin.MoreSeeThrough);
        yield return ("Pin Any Window: less see-through", S.WindowPin.LessSeeThrough);
        yield return ("Text Tools", S.TextTools.Menu);
        yield return ("Text Tools: paste as plain text", S.TextTools.PlainPaste);
        var w = S.Snapper;
        foreach (var (name, bind) in new[] { ("left", w.Left), ("right", w.Right), ("top", w.Top), ("bottom", w.Bottom), ("top-left", w.TopLeft), ("top-right", w.TopRight),
                     ("bottom-left", w.BottomLeft), ("bottom-right", w.BottomRight), ("maximize", w.Maximize), ("centre", w.Center), ("next monitor", w.NextMonitor), ("restore", w.Restore) })
            yield return ($"Window Snapper: {name}", bind);
        yield return ("Grid Jump", S.GridJump.Hotkey);
        yield return ("Symbol Lock", S.SymbolLock.Toggle);
        yield return ("Screenshot", S.Screenshot.Hotkey);
        yield return ("Quick Notes", S.Notes.Hotkey);
    }

    // ---------- App Volume ----------

    FrameworkElement BuildAppVolumePage()
    {
        var a = S.AppVolume;
        var page = new StackPanel();
        page.Children.Add(PageHeader("App Volume",
            "Turn just the app in front up or down. Make the game quieter and keep Discord loud, without opening the volume mixer.",
            Switch("Enabled", () => a.Enabled, v => a.Enabled = v)));

        page.Children.Add(Card("Hotkeys", "They change the app whose window is in front.", Stack(
            KeyRow("Louder", () => a.Up, v => a.Up = v, combo: true),
            KeyRow("Quieter", () => a.Down, v => a.Down = v, combo: true),
            KeyRow("Mute / unmute", () => a.Mute, v => a.Mute = v, combo: true),
            SliderRow("Step", "How much each press changes", 5, 25, 5, () => a.StepPercent, v => a.StepPercent = (int)v, v => $"{v:0}%"))));

        var mixer = new StackPanel();
        var refresh = new Button { Style = St("GhostButton"), Content = "Refresh", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        void Fill()
        {
            mixer.Children.Clear();
            List<AppVolumeModule.AppSession> sessions;
            try { sessions = AppVolumeModule.Sessions(); }
            catch (Exception ex) { mixer.Children.Add(Label($"Couldn't read the sound mixer: {ex.Message}", 12.5, muted: true)); return; }
            if (sessions.Count == 0) mixer.Children.Add(Label("No apps are playing sound right now.", 12.5, muted: true));
            foreach (var s in sessions)
            {
                var app = s.App;
                mixer.Children.Add(SliderRow(app, s.Muted ? "muted" : "", 0, 1, 0.05, () => s.Volume,
                    v => { try { AppVolumeModule.SetVolume(app, (float)v); } catch { } }, v => $"{v * 100:0}%"));
            }
        }
        refresh.Click += (_, _) => Fill();
        page.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) Fill(); };
        page.Children.Add(Card("Apps making sound", "A mini volume mixer.", Stack(mixer, refresh)));
        return page;
    }

    // ---------- Pin Any Window ----------

    FrameworkElement BuildPinPage()
    {
        var p = S.WindowPin;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Pin Any Window",
            "Keep any window on top of everything, like a guide, a video or a calculator while you play or work. You can make it see-through too.",
            Switch("Enabled", () => p.Enabled, v => p.Enabled = v)));

        page.Children.Add(Card("Hotkeys", "They work on the window in front.", Stack(
            KeyRow("Pin / unpin on top", () => p.Toggle, v => p.Toggle = v, combo: true),
            KeyRow("More see-through", () => p.MoreSeeThrough, v => p.MoreSeeThrough = v, combo: true),
            KeyRow("Less see-through", () => p.LessSeeThrough, v => p.LessSeeThrough = v, combo: true))));

        var list = new StackPanel();
        _refreshers.Add(() =>
        {
            if (!page.IsVisible) return;
            list.Children.Clear();
            var pinned = _e.WindowPin.Pinned;
            if (pinned.Count == 0) list.Children.Add(Label($"Nothing pinned yet. Click a window and press {p.Toggle}.", 12.5, muted: true));
            foreach (var (handle, title) in pinned)
            {
                var row = Cols(Star, GridLength.Auto);
                row.Margin = new Thickness(0, 4, 0, 4);
                Place(row, Label(string.IsNullOrWhiteSpace(title) ? "(untitled window)" : title), 0);
                var unpin = new Button { Style = St("GhostButton"), Content = "Unpin" };
                unpin.Click += (_, _) => { _e.WindowPin.Unpin(handle); RefreshStatus(); };
                Place(row, unpin, 1);
                list.Children.Add(row);
            }
        });
        page.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) RefreshStatus(); };
        page.Children.Add(Card("Pinned right now", null, list));
        return page;
    }

    // ---------- Text Tools ----------

    FrameworkElement BuildTextToolsPage()
    {
        var t = S.TextTools;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Text Tools",
            "Select text in any app, press the hotkey and pick a tool. Your text is changed right where it is, and your clipboard is put back afterwards.",
            Switch("Enabled", () => t.Enabled, v => t.Enabled = v)));
        page.Children.Add(Card("Hotkeys", null, Stack(
            KeyRow("Open the Text Tools menu", () => t.Menu, v => t.Menu = v, combo: true),
            KeyRow("Paste as plain text (no fonts or colours)", () => t.PlainPaste, v => t.PlainPaste = v, combo: true))));

        var tools = new StackPanel();
        string[] about =
        [
            "MAKES EVERYTHING CAPITALS", "makes everything small letters", "Makes Every Word Start Big", "Only the first letter of each sentence.",
            "Squashes double spaces into one.", "Turns several lines into one line.", "Puts lines in alphabetical order.", "Shows how many words and characters.",
        ];
        for (int i = 0; i < TextToolsModule.Tools.Length; i++)
        {
            var row = Cols(new GridLength(34), new GridLength(200), Star);
            row.Margin = new Thickness(0, 4, 0, 4);
            var n = Label((i + 1).ToString(), 13, bold: true);
            n.Foreground = Res("AccentBrush");
            Place(row, n, 0);
            Place(row, Label(TextToolsModule.Tools[i].Name, 13, bold: true), 1);
            Place(row, Label(about[i], 12.5, muted: true), 2);
            tools.Children.Add(row);
        }
        page.Children.Add(Card("The tools", "Press the number, or click it in the menu.", tools));
        return page;
    }

    // ---------- Text Expander ----------

    readonly StackPanel _expansionList = new();

    FrameworkElement BuildExpanderPage()
    {
        var x = S.Expander;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Text Expander",
            "Type a short code anywhere and it turns into the full text. Type ;mail and get your email address.",
            Switch("Enabled", () => x.Enabled, v => x.Enabled = v)));

        page.Children.Add(Card("Tips", null, Label(
            "• Start codes with ; so they never fire by accident while you type normally.\n" +
            "• You can use {date}, {time} and {clipboard} in the text.\n" +
            "• It doesn't run while mouse mode is on. Only the last few letters are watched, in memory, and nothing you type is saved.", 13, muted: true)));

        var add = new Button { Style = St("AccentButton"), Content = "+  New expansion" };
        add.Click += (_, _) =>
        {
            x.Items.Add(new ExpansionEntry { Abbreviation = ";", Text = "" });
            RebuildExpansions();
            Changed();
            PageScroller.ScrollToEnd();
        };
        var count = Label("", 13, muted: true);
        _refreshers.Add(() => count.Text = $"{x.Items.Count} expansions");
        var bar = Cols(Star, GridLength.Auto);
        bar.Margin = new Thickness(0, 0, 0, 14);
        Place(bar, count, 0);
        Place(bar, add, 1);
        page.Children.Add(bar);
        page.Children.Add(_expansionList);
        RebuildExpansions();
        return page;
    }

    void RebuildExpansions()
    {
        _expansionList.Children.Clear();
        foreach (var entry in S.Expander.Items.ToList())
        {
            var e = entry;
            var on = new CheckBox { Style = St("Switch"), IsChecked = e.Enabled, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) };
            RoutedEventHandler toggle = (_, _) => { e.Enabled = on.IsChecked == true; _e.SaveSoon(); };
            on.Checked += toggle;
            on.Unchecked += toggle;
            var code = new TextBox { Text = e.Abbreviation, Width = 130, FontFamily = (System.Windows.Media.FontFamily)FindResource("KeyFont"), Margin = new Thickness(14, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top, ToolTip = "What you type" };
            code.TextChanged += (_, _) => { e.Abbreviation = code.Text; _e.SaveSoon(); };
            var arrow = Label("→", 16, muted: true);
            arrow.VerticalAlignment = VerticalAlignment.Top;
            arrow.Margin = new Thickness(0, 4, 10, 0);
            var text = new TextBox { Text = e.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 34, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = "What it turns into" };
            text.TextChanged += (_, _) => { e.Text = text.Text; _e.SaveSoon(); };
            var del = new Button { Style = St("GhostButton"), Content = IconGlyph(""), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top, ToolTip = "Delete" };
            del.Click += (_, _) => { S.Expander.Items.Remove(e); RebuildExpansions(); Changed(); };

            var row = Cols(GridLength.Auto, GridLength.Auto, GridLength.Auto, Star, GridLength.Auto);
            Place(row, on, 0);
            Place(row, code, 1);
            Place(row, arrow, 2);
            Place(row, text, 3);
            Place(row, del, 4);
            _expansionList.Children.Add(new Border { Style = St("Card"), Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, 0, 10), Child = row });
        }
    }

    // ---------- Window Snapper ----------

    FrameworkElement BuildSnapperPage()
    {
        var w = S.Snapper;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Window Snapper",
            "Move the window in front to halves, thirds, corners or the next screen with your keyboard. Press Left or Right again to switch between ½, ⅓ and ⅔.",
            Switch("Enabled", () => w.Enabled, v => w.Enabled = v)));

        var left = new StackPanel();
        left.Children.Add(SectionLabel("Halves", first: true));
        left.Children.Add(KeyRow("Left", () => w.Left, v => w.Left = v, combo: true));
        left.Children.Add(KeyRow("Right", () => w.Right, v => w.Right = v, combo: true));
        left.Children.Add(KeyRow("Top", () => w.Top, v => w.Top = v, combo: true));
        left.Children.Add(KeyRow("Bottom", () => w.Bottom, v => w.Bottom = v, combo: true));
        left.Children.Add(SectionLabel("Whole screen"));
        left.Children.Add(KeyRow("Maximize", () => w.Maximize, v => w.Maximize = v, combo: true));
        left.Children.Add(KeyRow("Centre", () => w.Center, v => w.Center = v, combo: true));

        var right = new StackPanel();
        right.Children.Add(SectionLabel("Corners", first: true));
        right.Children.Add(KeyRow("Top-left", () => w.TopLeft, v => w.TopLeft = v, combo: true));
        right.Children.Add(KeyRow("Top-right", () => w.TopRight, v => w.TopRight = v, combo: true));
        right.Children.Add(KeyRow("Bottom-left", () => w.BottomLeft, v => w.BottomLeft = v, combo: true));
        right.Children.Add(KeyRow("Bottom-right", () => w.BottomRight, v => w.BottomRight = v, combo: true));
        right.Children.Add(SectionLabel("Other"));
        right.Children.Add(KeyRow("Move to next screen", () => w.NextMonitor, v => w.NextMonitor = v, combo: true));
        right.Children.Add(KeyRow("Put it back", () => w.Restore, v => w.Restore = v, combo: true));

        var binds = Cols(Star, new GridLength(36), Star);
        Place(binds, left, 0);
        Place(binds, right, 2);
        page.Children.Add(Card("Keybinds", "Corners use Home / Page Up / End / Page Down, laid out like the screen corners.", binds));
        page.Children.Add(Card("Options", null, Stack(
            Switch("Press again to cycle ½ → ⅓ → ⅔", () => w.CycleSizes, v => w.CycleSizes = v),
            SliderRow("Gap", "Space around snapped windows", 0, 30, 2, () => w.GapPixels, v => w.GapPixels = (int)v, v => $"{v:0} px"))));
        return page;
    }

    // ---------- Grid Jump ----------

    FrameworkElement BuildGridJumpPage()
    {
        var g = S.GridJump;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Grid Jump",
            "The screen turns into a lettered grid. Type two letters (row, then column) and the mouse jumps right there. Pairs great with Keyboard Mouse.",
            Switch("Enabled", () => g.Enabled, v => g.Enabled = v)));
        var tryIt = new Button { Style = St("AccentButton"), Content = "Try it", HorizontalAlignment = HorizontalAlignment.Left };
        tryIt.Click += (_, _) => _e.GridJump.Open();
        page.Children.Add(Card("Open the grid", null, Stack(
            KeyRow("Hotkey", () => g.Hotkey, v => g.Hotkey = v, combo: true),
            KeyRow("Key while mouse mode is on", () => g.MouseModeKey, v => g.MouseModeKey = v),
            tryIt)));
        page.Children.Add(Card("Options", null, Stack(
            SliderRow("Cell size", "Smaller cells = more precise, more letters", 50, 160, 10, () => g.CellSize, v => g.CellSize = v, v => $"{v:0}"),
            Switch("Left-click after jumping", () => g.ClickAfterJump, v => g.ClickAfterJump = v))));
        return page;
    }

    // ---------- Symbol Lock ----------

    FrameworkElement BuildSymbolLockPage()
    {
        var y = S.SymbolLock;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Symbol Lock",
            "Caps Lock for symbols. While it's on, the number row types ! @ # $ % … without holding Shift. Hold Shift to get the numbers back, just like Caps Lock does for letters.",
            Switch("Enabled", () => y.Enabled, v => { y.Enabled = v; if (!v) _e.SymbolLock.SetActive(false); })));

        page.Children.Add(StatusCard(
            () => _e.SymbolLock.Active,
            () => _e.SymbolLock.Active ? "Symbol Lock is ON" : "Symbol Lock is off",
            () => $"Press {y.Toggle} to switch it. With it on, 1 2 3 4 5 6 7 8 9 0 type:  {SymbolLockModule.Preview()}",
            () => _e.SymbolLock.SetActive(!_e.SymbolLock.Active),
            () => y.Enabled));

        page.Children.Add(Card("Hotkey", null, KeyRow("Turn Symbol Lock on / off", () => y.Toggle, v => y.Toggle = v, combo: true)));
        page.Children.Add(Card("Options", null, Stack(
            Switch("Only the number row (leave , . / ; ' [ ] - = alone)", () => y.NumbersOnly, v => y.NumbersOnly = v),
            Switch("Show a small \"Symbol Lock on\" badge while it's on", () => y.ShowIndicator, v => y.ShowIndicator = v))));
        return page;
    }

    // ---------- Screenshot ----------

    FrameworkElement BuildScreenshotPage()
    {
        var c = S.Screenshot;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Screenshot",
            "Grab part of the screen, blur anything private, draw boxes, then copy it or save it as a picture.",
            Switch("Enabled", () => c.Enabled, v => c.Enabled = v)));

        var now = new Button { Style = St("AccentButton"), Content = "Take one now", HorizontalAlignment = HorizontalAlignment.Left };
        now.Click += async (_, _) =>
        {
            WindowState = WindowState.Minimized; // get ourselves out of the picture
            await Task.Delay(350);
            _screenshotWindow.Open();
        };
        page.Children.Add(Card("Hotkey", null, Stack(KeyRow("Take a screenshot", () => c.Hotkey, v => c.Hotkey = v, combo: true), now)));

        page.Children.Add(Card("How it works", null, Label(
            "1. Drag a box over what you want (or press Enter for the whole screen).\n" +
            "2. Drag inside it to Blur (B) private parts, or draw a red Box (R). Ctrl+Z undoes.\n" +
            "3. Press Enter to copy it, or Ctrl+S to save it. Esc cancels.", 13, muted: true)));

        string DefaultFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Potato Keys+ Screenshots");
        var folder = SettingBox(string.IsNullOrWhiteSpace(c.SaveFolder) ? DefaultFolder() : c.SaveFolder, v => c.SaveFolder = v);
        var browse = new Button { Style = St("GhostButton"), Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Where should screenshots go?" };
            if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName;
        };
        var open = new Button { Style = St("GhostButton"), Content = "Open", Margin = new Thickness(8, 0, 0, 0) };
        open.Click += (_, _) =>
        {
            var path = string.IsNullOrWhiteSpace(c.SaveFolder) ? DefaultFolder() : c.SaveFolder;
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        };
        var folderRow = Cols(Star, GridLength.Auto, GridLength.Auto);
        Place(folderRow, folder, 0);
        Place(folderRow, browse, 1);
        Place(folderRow, open, 2);
        page.Children.Add(Card("Saving", null, Stack(
            Switch("Also save every screenshot I copy", () => c.AlwaysSave, v => c.AlwaysSave = v),
            folderRow)));
        return page;
    }

    // ---------- Quick Notes ----------

    FrameworkElement BuildNotesPage()
    {
        var n = S.Notes;
        var page = new StackPanel();
        page.Children.Add(PageHeader("Quick Notes",
            "A notepad that pops up with a hotkey and saves everything by itself. Press the hotkey again (or Esc) to hide it.",
            Switch("Enabled", () => n.Enabled, v => n.Enabled = v)));
        var open = new Button { Style = St("AccentButton"), Content = "Open notes", HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += (_, _) => _notesWindow.Toggle();
        var folder = new Button { Style = St("GhostButton"), Content = "Open notes folder", HorizontalAlignment = HorizontalAlignment.Left };
        folder.Click += (_, _) =>
        {
            Directory.CreateDirectory(QuickNotesWindow.NotesFolder);
            Process.Start(new ProcessStartInfo(QuickNotesWindow.NotesFolder) { UseShellExecute = true });
        };
        page.Children.Add(Card("Hotkey", null, Stack(KeyRow("Show / hide notes", () => n.Hotkey, v => n.Hotkey = v, combo: true), open)));
        page.Children.Add(Card("Options", "Notes are plain .txt files, so you can open them in any app too.", Stack(
            Switch("Keep the notes window on top of other windows", () => n.OnTop, v => { n.OnTop = v; _notesWindow.Topmost = v; }),
            folder)));
        return page;
    }
}
