using System.IO;
using System.Windows;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Modules;

namespace PotatoKeysPlus;

public partial class App : Application
{
    const string InstanceName = "PotatoKeysPlus.SingleInstance";

    Mutex? _singleInstance;
    EventWaitHandle? _showSignal;
    Engine? _engine;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, InstanceName, out bool firstInstance);
        if (!firstInstance)
        {
            // Already running (maybe hidden in the tray): ask it to show itself and quit.
            try { EventWaitHandle.OpenExisting(InstanceName + ".Show").Set(); } catch { }
            _singleInstance = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            MessageBox.Show(args.Exception.Message, "Potato Keys+ hit a snag", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => { if (args.ExceptionObject is Exception ex) LogCrash(ex); };

        var settings = SettingsStore.Load();
        _engine = new Engine(settings);
        var window = new MainWindow(_engine);
        MainWindow = window;

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.BeginInvoke(window.ShowFromTray);
        }) { IsBackground = true };
        listener.Start();

        // "--minimized" is used when starting with Windows: go straight to the tray.
        bool minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
        if (!minimized || !settings.CloseToTray)
        {
            window.Show();
            if (minimized) window.WindowState = WindowState.Minimized;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Dispose();
        _singleInstance?.ReleaseMutex();
        base.OnExit(e);
    }

    static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.AppendAllText(Path.Combine(SettingsStore.Folder, "crash.log"), $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { }
    }
}
