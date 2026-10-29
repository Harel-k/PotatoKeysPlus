using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus;

/// <summary>
/// Quick Notes: a small notepad that pops up on a hotkey and saves by itself.
/// Each note is a plain .txt file in %APPDATA%\PotatoKeysPlus\notes.
/// </summary>
public sealed class QuickNotesWindow : Window
{
    public static string NotesFolder => System.IO.Path.Combine(SettingsStore.Folder, "notes");

    readonly NotesSettings _settings;
    readonly Action _save;
    readonly TextBox _box;
    readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal };
    readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromMilliseconds(600) };
    readonly System.Windows.Controls.Primitives.ToggleButton _pin;
    List<string> _files = [];
    int _current;

    public QuickNotesWindow(NotesSettings settings, Action save)
    {
        _settings = settings;
        _save = save;
        Title = "Quick Notes";
        Width = settings.Width;
        Height = settings.Height;
        MinWidth = 260;
        MinHeight = 180;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Topmost = settings.OnTop;
        Background = (Brush)FindResource("BgBrush");
        BorderBrush = (Brush)FindResource("AccentBrush");
        BorderThickness = new Thickness(1);
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 38, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        if (settings.Left is double l && settings.Top is double t) { WindowStartupLocation = WindowStartupLocation.Manual; Left = l; Top = t; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var title = new TextBlock { Text = "Quick Notes", FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
        _pin = new System.Windows.Controls.Primitives.ToggleButton { Style = (Style)FindResource("TitleToggle"), Content = "", Height = 38, Width = 38, IsChecked = settings.OnTop, ToolTip = "Keep on top" };
        _pin.Checked += (_, _) => SetOnTop(true);
        _pin.Unchecked += (_, _) => SetOnTop(false);
        var close = new Button { Style = (Style)FindResource("TitleButton"), Content = "", Height = 38, Width = 40, ToolTip = "Hide (Esc)" };
        close.Click += (_, _) => HideNotes();

        var header = new DockPanel { Background = (Brush)FindResource("PanelBrush"), Height = 38 };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(_pin, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(_pin);
        header.Children.Add(title);
        header.Children.Add(_tabs);

        _box = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = settings.FontSize, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            Foreground = (Brush)FindResource("TextBrush"), CaretBrush = (Brush)FindResource("AccentBrush"), Padding = new Thickness(12),
        };
        SpellCheck.SetIsEnabled(_box, true);
        _box.TextChanged += (_, _) => { _autosave.Stop(); _autosave.Start(); };
        _autosave.Tick += (_, _) => { _autosave.Stop(); SaveCurrent(); };

        var footer = new TextBlock
        {
            Text = "Saves by itself  ·  Ctrl+N new note  ·  Ctrl + / Ctrl − text size  ·  Esc hides",
            Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11, Margin = new Thickness(12, 4, 12, 6),
        };
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(_box);
        Content = root;

        PreviewKeyDown += OnKey;
        Closing += (_, e) => { e.Cancel = true; HideNotes(); };
        LoadFiles();
    }

    public void Toggle()
    {
        if (IsVisible && IsActive) { HideNotes(); return; }
        Show();
        Activate();
        Native.ForceForeground(new WindowInteropHelper(this).Handle);
        _box.Focus();
        _box.CaretIndex = _box.Text.Length;
    }

    public void HideNotes()
    {
        SaveCurrent();
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Width = ActualWidth > 0 ? ActualWidth : Width;
        _settings.Height = ActualHeight > 0 ? ActualHeight : Height;
        _save();
        Hide();
    }

    void SetOnTop(bool on)
    {
        _settings.OnTop = on;
        Topmost = on;
        _pin.Content = on ? "" : "";
        _save();
    }

    void LoadFiles()
    {
        Directory.CreateDirectory(NotesFolder);
        _files = Directory.GetFiles(NotesFolder, "note-*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        if (_files.Count == 0)
        {
            var first = System.IO.Path.Combine(NotesFolder, "note-001.txt");
            File.WriteAllText(first, "");
            _files.Add(first);
        }
        Open(Math.Clamp(_settings.LastNote, 0, _files.Count - 1));
    }

    void Open(int index)
    {
        _current = index;
        _settings.LastNote = index;
        _box.Text = File.Exists(_files[index]) ? File.ReadAllText(_files[index]) : "";
        _autosave.Stop();
        BuildTabs();
    }

    void BuildTabs()
    {
        _tabs.Children.Clear();
        for (int i = 0; i < _files.Count; i++)
        {
            int index = i;
            var tab = new Button
            {
                Style = (Style)FindResource(i == _current ? "AccentButton" : "GhostButton"), Content = (i + 1).ToString(),
                Padding = new Thickness(9, 2, 9, 3), Margin = new Thickness(0, 6, 4, 6), MinWidth = 28,
                ToolTip = Preview(_files[i]),
            };
            WindowChrome.SetIsHitTestVisibleInChrome(tab, true);
            tab.Click += (_, _) => { SaveCurrent(); Open(index); _box.Focus(); };
            _tabs.Children.Add(tab);
        }
        var add = new Button { Style = (Style)FindResource("GhostButton"), Content = "+", Padding = new Thickness(9, 2, 9, 3), Margin = new Thickness(0, 6, 4, 6), ToolTip = "New note (Ctrl+N)" };
        WindowChrome.SetIsHitTestVisibleInChrome(add, true);
        add.Click += (_, _) => NewNote();
        _tabs.Children.Add(add);
        if (_files.Count > 1)
        {
            var del = new Button { Style = (Style)FindResource("GhostButton"), Content = "", FontFamily = (FontFamily)FindResource("IconFont"), Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(0, 6, 4, 6), ToolTip = "Delete this note" };
            WindowChrome.SetIsHitTestVisibleInChrome(del, true);
            del.Click += (_, _) => DeleteCurrent();
            _tabs.Children.Add(del);
        }
    }

    static string Preview(string file)
    {
        try
        {
            var line = File.ReadLines(file).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "(empty)";
            return line.Length > 60 ? line[..60] + "…" : line;
        }
        catch
        {
            return "";
        }
    }

    void NewNote()
    {
        SaveCurrent();
        int next = _files.Select(f => int.TryParse(System.IO.Path.GetFileNameWithoutExtension(f)[5..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var file = System.IO.Path.Combine(NotesFolder, $"note-{next:000}.txt");
        File.WriteAllText(file, "");
        _files.Add(file);
        Open(_files.Count - 1);
        _box.Focus();
    }

    void DeleteCurrent()
    {
        if (_box.Text.Trim().Length > 0
            && MessageBox.Show(this, "Delete this note? This can't be undone.", "Quick Notes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        _autosave.Stop();
        try { File.Delete(_files[_current]); } catch { }
        _files.RemoveAt(_current);
        if (_files.Count == 0) { LoadFiles(); return; }
        Open(Math.Min(_current, _files.Count - 1));
    }

    void SaveCurrent()
    {
        if (_current < 0 || _current >= _files.Count) return;
        try { File.WriteAllText(_files[_current], _box.Text); } catch { }
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Escape) HideNotes();
        else if (ctrl && e.Key == Key.N) NewNote();
        else if (ctrl && e.Key is Key.OemPlus or Key.Add) Zoom(1);
        else if (ctrl && e.Key is Key.OemMinus or Key.Subtract) Zoom(-1);
        else return;
        e.Handled = true;
    }

    void Zoom(int step)
    {
        _settings.FontSize = Math.Clamp(_settings.FontSize + step, 10, 32);
        _box.FontSize = _settings.FontSize;
        _save();
    }

    /// <summary>Saves the open note before the app quits or settings are restored from a backup.</summary>
    public void Flush() => SaveCurrent();
}
