using Forms = System.Windows.Forms;

namespace PotatoKeysPlus;

/// <summary>Notification-area icon so Potato Keys+ keeps running after the window is closed.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly Forms.ToolStripMenuItem _mouseItem;
    readonly Forms.ToolStripMenuItem _pinItem;
    Action? _balloonClick;

    public TrayIcon(Action open, Action toggleMouse, Action togglePin, Action checkUpdates, Action quit)
    {
        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Open Potato Keys+", null, (_, _) => open());
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        _mouseItem = new Forms.ToolStripMenuItem("Mouse mode", null, (_, _) => toggleMouse());
        _pinItem = new Forms.ToolStripMenuItem("Pin window on top", null, (_, _) => togglePin());
        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_mouseItem);
        menu.Items.Add(_pinItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Check for updates", null, (_, _) => checkUpdates()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit", null, (_, _) => quit()));

        _icon = new Forms.NotifyIcon
        {
            Text = "Potato Keys+",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) open(); };
        _icon.BalloonTipClicked += (_, _) => _balloonClick?.Invoke();
    }

    public void SetState(bool mouseMode, bool pinned)
    {
        _mouseItem.Checked = mouseMode;
        _pinItem.Checked = pinned;
        _icon.Text = mouseMode ? "Potato Keys+ (mouse mode ON)" : "Potato Keys+";
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
    }
}
