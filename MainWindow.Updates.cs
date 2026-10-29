using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Services;

namespace PotatoKeysPlus;

// Auto-updates and the "Settings & updates" page.
public partial class MainWindow
{
    enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Failed }

    UpdateState _updateState;
    string _updateMessage = "";
    double _updateProgress;
    int _lastProgressPercent = -1;
    UpdateInfo? _update;
    Ellipse? _updateDot;
    DispatcherTimer? _updateTimer;

    void StartUpdateChecks()
    {
        // Check shortly after start, then every 6 hours while running.
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        first.Tick += (_, _) =>
        {
            first.Stop();
            if (S.AutoCheckUpdates) _ = CheckForUpdatesAsync(userInitiated: false);
        };
        first.Start();

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += (_, _) => { if (S.AutoCheckUpdates) _ = CheckForUpdatesAsync(userInitiated: false); };
        _updateTimer.Start();
    }

    async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_updateState is UpdateState.Checking or UpdateState.Downloading) return;
        SetUpdateState(UpdateState.Checking, "Checking for updates…");
        try
        {
            var update = await Updater.CheckAsync();
            S.LastUpdateCheck = DateTime.Now;
            _e.SaveSoon();
            if (update == null)
            {
                SetUpdateState(UpdateState.UpToDate, $"You're on the latest version (v{Updater.CurrentVersion}).");
                return;
            }

            bool isNew = _update?.Version != update.Version;
            _update = update;
            SetUpdateState(UpdateState.Available, $"Potato Keys+ {update.Version} is out!");
            if (isNew && !IsVisible)
                _tray?.Notify("Update available", $"Potato Keys+ {update.Version} is ready. Click here to update.",
                    () => { ShowFromTray(); SelectModule("about"); });
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Failed, userInitiated
                ? $"Couldn't check for updates: {ex.Message}"
                : "Couldn't reach GitHub to check for updates.");
        }
    }

    async Task InstallUpdateAsync()
    {
        if (_update == null || _updateState == UpdateState.Downloading) return;
        _lastProgressPercent = -1;
        _updateProgress = 0;
        SetUpdateState(UpdateState.Downloading, $"Downloading version {_update.Version}…");
        try
        {
            var progress = new Progress<double>(p =>
            {
                _updateProgress = p;
                int percent = (int)(p * 100);
                if (percent == _lastProgressPercent) return;
                _lastProgressPercent = percent;
                _updateMessage = $"Downloading version {_update.Version}… {percent}%";
                RefreshStatus();
            });
            var installer = await Updater.DownloadAsync(_update, progress);
            SetUpdateState(UpdateState.Downloading, "Installing… Potato Keys+ will restart by itself.");
            Updater.LaunchInstaller(installer);
            QuitApp();
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Available, $"The update didn't finish: {ex.Message}");
        }
    }

    void SetUpdateState(UpdateState state, string message)
    {
        _updateState = state;
        _updateMessage = message;
        RefreshStatus();
    }

    void UpdateBannerButton_Click(object sender, RoutedEventArgs e) => _ = InstallUpdateAsync();

    void RefreshUpdateBanner()
    {
        bool show = _update != null && _updateState is UpdateState.Available or UpdateState.Downloading;
        UpdateBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_updateDot != null) _updateDot.Visibility = _update != null ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;
        UpdateBannerText.Text = _updateState == UpdateState.Downloading
            ? _updateMessage
            : $"Potato Keys+ {_update!.Version} is available. You have v{Updater.CurrentVersion}.";
        UpdateBannerButton.IsEnabled = _updateState != UpdateState.Downloading;
        UpdateBannerButton.Content = _updateState == UpdateState.Downloading ? "Updating…" : "Update now";
    }

    FrameworkElement BuildAboutPage()
    {
        var page = new StackPanel();
        page.Children.Add(PageHeader("Settings & updates", "App options, updates and info about Potato Keys+.", null));

        // Updates
        var version = Label($"v{Updater.CurrentVersion}", 22, bold: true);
        var status = Label("", 13, muted: true);
        status.Margin = new Thickness(0, 4, 0, 0);
        var lastChecked = Label("", 11.5, muted: true);
        lastChecked.Margin = new Thickness(0, 2, 0, 0);
        var info = new StackPanel();
        info.Children.Add(version);
        info.Children.Add(status);
        info.Children.Add(lastChecked);

        var check = new Button { Style = St("GhostButton"), Content = "Check for updates", VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => _ = CheckForUpdatesAsync(userInitiated: true);
        var install = new Button { Style = St("AccentButton"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        install.Click += (_, _) => _ = InstallUpdateAsync();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(check);
        buttons.Children.Add(install);

        var top = Cols(Star, GridLength.Auto);
        Place(top, info, 0);
        Place(top, buttons, 1);

        var bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 14, 0, 0) };
        var notes = Label("", 12.5, muted: true);
        notes.Margin = new Thickness(0, 14, 0, 0);
        var auto = Switch("Check for updates automatically", () => S.AutoCheckUpdates, v => S.AutoCheckUpdates = v);
        auto.Margin = new Thickness(0, 16, 0, 0);

        var updates = new StackPanel();
        updates.Children.Add(top);
        updates.Children.Add(bar);
        updates.Children.Add(notes);
        updates.Children.Add(auto);
        page.Children.Add(Card("Updates", null, updates));

        _refreshers.Add(() =>
        {
            status.Text = _updateMessage.Length > 0 ? _updateMessage : "Updates come from the official GitHub releases.";
            status.Foreground = Res(_updateState == UpdateState.Available ? "AccentBrush" : "MutedBrush");
            lastChecked.Text = S.LastUpdateCheck is DateTime t ? $"Last checked {t:g}" : "Not checked yet";
            check.IsEnabled = _updateState is not (UpdateState.Checking or UpdateState.Downloading);
            install.Visibility = _update != null ? Visibility.Visible : Visibility.Collapsed;
            install.IsEnabled = _updateState != UpdateState.Downloading;
            install.Content = _update != null ? $"Install v{_update.Version}" : "";
            bar.Visibility = _updateState == UpdateState.Downloading ? Visibility.Visible : Visibility.Collapsed;
            bar.Value = _updateProgress;
            var text = _update?.Notes.Trim() ?? "";
            notes.Text = text.Length > 600 ? text[..600] + "…" : text;
            notes.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        });

        // App behaviour
        var app = new StackPanel();
        app.Children.Add(Switch("Start Potato Keys+ when Windows starts", () => Startup.IsEnabled, v =>
        {
            try { Startup.Set(v); }
            catch (Exception ex) { MessageBox.Show(this, $"Couldn't change the startup setting: {ex.Message}", "Potato Keys+"); }
        }));
        var tray = Switch("Keep running in the tray when I close the window", () => S.CloseToTray, v => S.CloseToTray = v);
        tray.Margin = new Thickness(0, 12, 0, 0);
        app.Children.Add(tray);
        page.Children.Add(Card("App", "With the tray on, your hotkeys and the keyboard mouse keep working when the window is closed.", app));

        // About
        var links = new WrapPanel();
        links.Children.Add(LinkButton("GitHub page", Updater.RepoUrl));
        links.Children.Add(LinkButton("Report a bug / suggest a module", $"{Updater.RepoUrl}/issues/new"));
        links.Children.Add(LinkButton("Open settings folder", SettingsStore.Folder));
        var about = new StackPanel();
        about.Children.Add(Label("Potato Keys+ is made by Harel. It's free and open source under the MIT license.", 13, muted: true));
        links.Margin = new Thickness(0, 14, 0, 0);
        about.Children.Add(links);
        page.Children.Add(Card("About", null, about));

        return page;
    }

    Button LinkButton(string text, string target)
    {
        var button = new Button { Style = St("GhostButton"), Content = text, Margin = new Thickness(0, 0, 10, 8) };
        button.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Potato Keys+"); }
        };
        return button;
    }
}
