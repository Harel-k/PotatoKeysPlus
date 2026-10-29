using Microsoft.Win32;

namespace PotatoKeysPlus.Services;

/// <summary>"Start with Windows" through the per-user Run key (no admin needed).</summary>
public static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "PotatoKeysPlus";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
