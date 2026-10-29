using Forms = System.Windows.Forms;

namespace PotatoKeysPlus;

/// <summary>Notification-area icon so Potato Keys+ keeps running after the window is closed.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly Forms.ContextMenuStrip _menu = new();
    readonly List<(Forms.ToolStripMenuItem Item, Func<bool> IsOn)> _toggles = [];
    int _insertAt;
    Action? _balloonClick;

    public TrayIcon(Action open, Action checkUpdates, Action quit)
    {
        var openItem = new Forms.ToolStripMenuItem("Open Potato Keys+", null, (_, _) => open());
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        _menu.Items.Add(openItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _insertAt = _menu.Items.Count;
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(new Forms.ToolStripMenuItem("Check for updates", null, (_, _) => checkUpdates()));
        _menu.Items.Add(new Forms.ToolStripMenuItem("Quit", null, (_, _) => quit()));
        _menu.Opening += (_, _) => Refresh();

        _icon = new Forms.NotifyIcon
        {
            Text = "Potato Keys+",
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) open(); };
        _icon.BalloonTipClicked += (_, _) => _balloonClick?.Invoke();
    }

    /// <summary>Adds an on/off item (shown with a tick when on).</summary>
    public void AddToggle(string text, Func<bool> isOn, Action toggle)
    {
        var item = new Forms.ToolStripMenuItem(text, null, (_, _) => toggle());
        _menu.Items.Insert(_insertAt++, item);
        _toggles.Add((item, isOn));
    }

    /// <summary>Adds a plain action item next to the toggles.</summary>
    public void AddAction(string text, Action run) =>
        _menu.Items.Insert(_insertAt++, new Forms.ToolStripMenuItem(text, null, (_, _) => run()));

    public void Refresh(string? tooltip = null)
    {
        foreach (var (item, isOn) in _toggles) item.Checked = isOn();
        if (tooltip != null) _icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    public void Notify(string title, string text, Action? onClick = null)
    {
        _balloonClick = onClick;
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);
    }

    static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon)
                return icon;
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
