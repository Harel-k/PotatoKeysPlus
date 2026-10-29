using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using Path = System.Windows.Shapes.Path;

namespace PotatoKeysPlus;

/// <summary>
/// Screenshot Tool overlay: freezes the screen under the mouse, lets you drag a region, blur or
/// box parts of it, then copy (Enter / Ctrl+C) or save (Ctrl+S).
/// </summary>
public sealed class ScreenshotWindow : Window
{
    enum Tool { Blur, Box }

    readonly ScreenshotSettings _settings;
    readonly Action<string> _toast;
    readonly Image _image = new() { Stretch = Stretch.Fill };
    readonly Path _dim = new() { Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)), IsHitTestVisible = false };
    readonly Rectangle _frame = new() { Stroke = Brushes.White, StrokeThickness = 1.5, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    readonly Rectangle _draft = new() { StrokeThickness = 2, StrokeDashArray = [4, 3], IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    readonly StackPanel _toolbar = new() { Orientation = Orientation.Horizontal };
    readonly Border _toolbarHost;
    readonly TextBlock _hint;
    readonly Stack<(Int32Rect Rect, byte[] Pixels)> _undo = new();

    WriteableBitmap? _bitmap;
    Rect _selection = Rect.Empty;
    Point _dragStart;
    bool _dragging, _editing;
    Tool _tool = Tool.Blur;
    Button? _blurButton, _boxButton;

    public ScreenshotWindow(ScreenshotSettings settings, Action<string> toast)
    {
        _settings = settings;
        _toast = toast;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Black;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Cursor = Cursors.Cross;

        _hint = new TextBlock { Foreground = Brushes.White, FontSize = 13 };
        var hintHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x18, 0x19, 0x1E)), CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 7, 16, 8), Child = _hint, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 24, 0, 0), IsHitTestVisible = false,
        };
        _toolbarHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x1F, 0x21, 0x28)), CornerRadius = new CornerRadius(10),
            BorderBrush = (Brush)FindResource("CardBorderBrush"), BorderThickness = new Thickness(1),
            Padding = new Thickness(6), Child = _toolbar, Visibility = Visibility.Collapsed, Cursor = Cursors.Arrow,
        };
        BuildToolbar();

        _canvas.Children.Add(_frame);
        _canvas.Children.Add(_draft);
        _canvas.Children.Add(_toolbarHost);
        var root = new Grid();
        root.Children.Add(_image);
        root.Children.Add(_dim);
        root.Children.Add(_canvas);
        root.Children.Add(hintHost);
        Content = root;

        _canvas.MouseLeftButtonDown += OnDown;
        _canvas.MouseMove += OnMove;
        _canvas.MouseLeftButtonUp += OnUp;
        PreviewKeyDown += OnKey;
        Deactivated += (_, _) => { if (IsVisible) Finish(null); };
    }

    void BuildToolbar()
    {
        Button B(string glyph, string text, string tip, Action click)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = glyph, FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 14, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Style = (Style)FindResource("GhostButton"), Content = content, ToolTip = tip, Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(10, 6, 10, 6) };
            b.Click += (_, _) => click();
            _toolbar.Children.Add(b);
            return b;
        }
        _blurButton = B("", "Blur", "Drag over private parts to blur them (B)", () => SetTool(Tool.Blur));
        _boxButton = B("", "Box", "Drag to draw a red box (R)", () => SetTool(Tool.Box));
        B("", "Undo", "Ctrl + Z", Undo);
        B("", "Copy", "Copy to clipboard (Enter / Ctrl + C)", () => Finish(save: false));
        B("", "Save", "Save as PNG (Ctrl + S)", () => Finish(save: true));
        B("", "", "Cancel (Esc)", () => Finish(null));
        SetTool(Tool.Blur);
    }

    void SetTool(Tool tool)
    {
        _tool = tool;
        if (_blurButton == null || _boxButton == null) return;
        _blurButton.Style = (Style)FindResource(tool == Tool.Blur ? "AccentButton" : "GhostButton");
        _boxButton.Style = (Style)FindResource(tool == Tool.Box ? "AccentButton" : "GhostButton");
        _blurButton.Padding = _boxButton.Padding = new Thickness(10, 6, 10, 6);
    }

    // ---------- open / capture ----------

    public void Open()
    {
        if (IsVisible) return;
        Native.GetCursorPos(out var p);
        var screen = Native.MonitorInfo(Native.MonitorFromPoint(p, 2)).rcMonitor;
        try { _bitmap = Capture(screen); }
        catch (Exception)
        {
            // Happens while the PC is locked or a secure screen (like a UAC prompt) is showing.
            _toast("Can't take a screenshot right now");
            return;
        }
        _image.Source = _bitmap;
        _selection = Rect.Empty;
        _editing = false;
        _undo.Clear();
        _frame.Visibility = _toolbarHost.Visibility = _draft.Visibility = Visibility.Collapsed;
        _hint.Text = "Drag to pick an area   ·   Enter = whole screen   ·   Esc = cancel";

        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, screen.Left, screen.Top, screen.Width, screen.Height, Native.SWP_SHOWWINDOW);
        UpdateLayout();
        UpdateDim();
        Activate();
        Native.ForceForeground(hwnd);
        Focus();
    }

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr obj);

    static WriteableBitmap Capture(Native.RECT r)
    {
        using var bmp = new System.Drawing.Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(r.Width, r.Height));
        var h = bmp.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return new WriteableBitmap(new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0));
        }
        finally
        {
            DeleteObject(h);
        }
    }

    // ---------- mouse ----------

    void OnDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_canvas);
        _dragStart = p;
        _dragging = true;
        _canvas.CaptureMouse();
        if (_editing && !_selection.Contains(p))
        {
            // Clicking outside the area starts a new selection.
            _editing = false;
            _toolbarHost.Visibility = Visibility.Collapsed;
        }
    }

    void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var r = new Rect(_dragStart, e.GetPosition(_canvas));
        if (!_editing)
        {
            _selection = r;
            UpdateDim();
        }
        else
        {
            r.Intersect(_selection);
            if (r.IsEmpty) return;
            _draft.Stroke = _tool == Tool.Blur ? Brushes.White : (Brush)FindResource("DangerBrush");
            Place(_draft, r);
            _draft.Visibility = Visibility.Visible;
        }
    }

    void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _canvas.ReleaseMouseCapture();
        var r = new Rect(_dragStart, e.GetPosition(_canvas));
        if (!_editing)
        {
            if (r.Width < 6 || r.Height < 6) return;
            _selection = r;
            StartEditing();
        }
        else
        {
            _draft.Visibility = Visibility.Collapsed;
            r.Intersect(_selection);
            if (!r.IsEmpty && r.Width > 3 && r.Height > 3) Apply(r);
        }
    }

    void StartEditing()
    {
        _editing = true;
        UpdateDim();
        _hint.Text = "Drag inside to blur or box   ·   Enter = copy   ·   Ctrl + S = save   ·   Esc = cancel";
        _toolbarHost.Visibility = Visibility.Visible;
        _toolbarHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = Math.Clamp(_selection.Right - _toolbarHost.DesiredSize.Width, 8, ActualWidth - _toolbarHost.DesiredSize.Width - 8);
        double y = _selection.Bottom + 10 + _toolbarHost.DesiredSize.Height < ActualHeight ? _selection.Bottom + 10 : Math.Max(8, _selection.Top - _toolbarHost.DesiredSize.Height - 10);
        Canvas.SetLeft(_toolbarHost, x);
        Canvas.SetTop(_toolbarHost, y);
    }

    void UpdateDim()
    {
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        _dim.Data = _selection.IsEmpty ? full : new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(_selection));
        if (_selection.IsEmpty) { _frame.Visibility = Visibility.Collapsed; return; }
        Place(_frame, _selection);
        _frame.Visibility = Visibility.Visible;
    }

    static void Place(Rectangle shape, Rect r)
    {
        Canvas.SetLeft(shape, r.X);
        Canvas.SetTop(shape, r.Y);
        shape.Width = r.Width;
        shape.Height = r.Height;
    }

    // ---------- editing (straight on the pixels) ----------

    Int32Rect ToPixels(Rect r)
    {
        double sx = _bitmap!.PixelWidth / ActualWidth, sy = _bitmap.PixelHeight / ActualHeight;
        int x = Math.Clamp((int)(r.X * sx), 0, _bitmap.PixelWidth - 1), y = Math.Clamp((int)(r.Y * sy), 0, _bitmap.PixelHeight - 1);
        int w = Math.Clamp((int)(r.Width * sx), 1, _bitmap.PixelWidth - x), h = Math.Clamp((int)(r.Height * sy), 1, _bitmap.PixelHeight - y);
        return new Int32Rect(x, y, w, h);
    }

    void Apply(Rect area)
    {
        var px = ToPixels(area);
        int stride = px.Width * 4;
        var pixels = new byte[stride * px.Height];
        _bitmap!.CopyPixels(px, pixels, stride, 0);
        _undo.Push((px, (byte[])pixels.Clone()));
        if (_undo.Count > 20) { var keep = _undo.Take(20).Reverse().ToList(); _undo.Clear(); foreach (var u in keep) _undo.Push(u); }

        if (_tool == Tool.Blur)
        {
            int block = Math.Max(6, (int)(14 * _bitmap.PixelWidth / ActualWidth));
            for (int by = 0; by < px.Height; by += block)
            for (int bx = 0; bx < px.Width; bx += block)
            {
                int bw = Math.Min(block, px.Width - bx), bh = Math.Min(block, px.Height - by);
                long b = 0, g = 0, r = 0;
                for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    int i = (by + y) * stride + (bx + x) * 4;
                    b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2];
                }
                int n = bw * bh;
                for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    int i = (by + y) * stride + (bx + x) * 4;
                    pixels[i] = (byte)(b / n); pixels[i + 1] = (byte)(g / n); pixels[i + 2] = (byte)(r / n); pixels[i + 3] = 255;
                }
            }
        }
        else
        {
            int t = Math.Max(2, (int)(3 * _bitmap.PixelWidth / ActualWidth));
            for (int y = 0; y < px.Height; y++)
            for (int x = 0; x < px.Width; x++)
            {
                if (x >= t && x < px.Width - t && y >= t && y < px.Height - t) continue;
                int i = y * stride + x * 4;
                pixels[i] = 0x4B; pixels[i + 1] = 0x53; pixels[i + 2] = 0xE5; pixels[i + 3] = 255; // #E5534B
            }
        }
        _bitmap.WritePixels(px, pixels, stride, 0);
    }

    void Undo()
    {
        if (_bitmap == null || _undo.Count == 0) return;
        var (rect, pixels) = _undo.Pop();
        _bitmap.WritePixels(rect, pixels, rect.Width * 4, 0);
    }

    // ---------- keys / finish ----------

    void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.Escape: Finish(null); break;
            case Key.Enter when !_editing:
            case Key.A when !_editing:
                _selection = new Rect(0, 0, ActualWidth, ActualHeight);
                StartEditing();
                break;
            case Key.Enter: Finish(save: false); break;
            case Key.C when ctrl: Finish(save: false); break;
            case Key.S when ctrl: Finish(save: true); break;
            case Key.Z when ctrl: Undo(); break;
            case Key.B: SetTool(Tool.Blur); break;
            case Key.R: SetTool(Tool.Box); break;
            default: return;
        }
        e.Handled = true;
    }

    /// <summary>save: null = cancel, false = copy, true = save to file (and copy).</summary>
    void Finish(bool? save)
    {
        string? message = null;
        if (save != null && _bitmap != null && !_selection.IsEmpty)
        {
            var cropped = new CroppedBitmap(_bitmap, ToPixels(_selection));
            try
            {
                Clipboard.SetImage(cropped);
                message = "Screenshot copied";
                if (save == true || _settings.AlwaysSave)
                {
                    var path = SaveFile(cropped);
                    message = $"Saved to {path}";
                }
            }
            catch (Exception ex)
            {
                message = $"Screenshot failed: {ex.Message}";
            }
        }
        Hide();
        _image.Source = null;
        _bitmap = null; // free the big frozen image right away
        _undo.Clear();
        if (message != null) _toast(message);
    }

    string SaveFile(BitmapSource image)
    {
        var folder = string.IsNullOrWhiteSpace(_settings.SaveFolder)
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Potato Keys+ Screenshots")
            : _settings.SaveFolder;
        Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        encoder.Save(file);
        return path;
    }
}
