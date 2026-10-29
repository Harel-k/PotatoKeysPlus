using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Modules;

public enum LaunchKind { App, Command, Calculator, Website, File, Search }

public sealed record LaunchItem(string Id, string Title, string Subtitle, LaunchKind Kind, Action Run);

/// <summary>
/// Quick Launcher: a hotkey opens a search box. Type a few letters and press Enter to open an app,
/// website, file or Potato Keys+ command, do quick maths, or search the web.
/// </summary>
public sealed partial class LauncherModule(LauncherSettings settings, Engine engine)
{
    record AppEntry(string Name, string AppId);

    List<AppEntry> _apps = [];
    DateTime _indexedAt = DateTime.MinValue;
    Task? _indexing;

    public event Action? OpenRequested;
    public LauncherSettings Settings => settings;

    /// <summary>Extra commands (mouse mode, gamer mode, ...) supplied by the UI.</summary>
    public List<(string Name, string Hint, Action Run)> Commands { get; } = [];

    public bool IsIndexing => _indexing is { IsCompleted: false };

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled || !settings.Hotkey.Matches(vk, mods)) return false;
        engine.Post(() => OpenRequested?.Invoke());
        return true;
    }

    /// <summary>Re-reads the installed apps in the background when the list is a few minutes old.</summary>
    public Task RefreshIndexAsync()
    {
        if (IsIndexing) return _indexing!;
        if ((DateTime.Now - _indexedAt).TotalMinutes < 5 && _apps.Count > 0) return Task.CompletedTask;
        var done = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                _apps = ReadAppsFolder();
                _indexedAt = DateTime.Now;
            }
            catch { }
            done.SetResult();
        });
        thread.SetApartmentState(ApartmentState.STA); // Shell COM needs STA
        thread.IsBackground = true;
        thread.Start();
        return _indexing = done.Task;
    }

    // The "Apps" folder lists every Start menu app: normal programs and Store apps alike.
    static List<AppEntry> ReadAppsFolder()
    {
        var result = new List<AppEntry>();
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
        dynamic folder = shell.NameSpace("shell:AppsFolder");
        dynamic items = folder.Items();
        int count = items.Count;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            dynamic item = items.Item(i);
            string name = item.Name;
            string id = item.Path;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
            if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
            if (seen.Add(name)) result.Add(new AppEntry(name, id));
        }
        return result;
    }

    public List<LaunchItem> Search(string query, int max = 8)
    {
        query = query.Trim();
        var results = new List<(LaunchItem Item, double Score)>();

        if (query.Length == 0)
        {
            // Empty box: show what you open most.
            foreach (var app in _apps)
                if (settings.Usage.TryGetValue(AppKey(app), out int uses))
                    results.Add((AppItem(app), uses));
            return results.OrderByDescending(r => r.Score).Take(max).Select(r => r.Item).ToList();
        }

        if (settings.Calculator && TryCalculate(query, out var answer))
            results.Add((new LaunchItem("calc", $"= {answer}", "Press Enter to copy the answer", LaunchKind.Calculator,
                () => System.Windows.Clipboard.SetText(answer)), 10_000));

        if (LooksLikePath(query))
            results.Add((new LaunchItem("path", query, "Open this file or folder", LaunchKind.File, () => Open(query)), 9_000));
        else if (LooksLikeUrl(query))
        {
            var url = query.Contains("://") ? query : "https://" + query;
            results.Add((new LaunchItem("url", url, "Open website", LaunchKind.Website, () => Open(url)), 9_000));
        }

        foreach (var app in _apps)
        {
            double score = Score(app.Name, query);
            if (score <= 0) continue;
            score += Math.Min(settings.Usage.GetValueOrDefault(AppKey(app)), 30) * 15;
            results.Add((AppItem(app), score));
        }

        foreach (var (name, hint, run) in Commands)
        {
            double score = Score(name, query);
            if (score > 0) results.Add((new LaunchItem("cmd:" + name, name, hint, LaunchKind.Command, run), score + 50));
        }

        var list = results.OrderByDescending(r => r.Score).Take(max).Select(r => r.Item).ToList();
        if (settings.WebSearch && list.Count < max)
        {
            var url = string.Format(CultureInfo.InvariantCulture, settings.SearchUrl, Uri.EscapeDataString(query));
            list.Add(new LaunchItem("web", $"Search the web for \"{query}\"", url, LaunchKind.Search, () => Open(url)));
        }
        return list;
    }

    public void Run(LaunchItem item)
    {
        if (item.Kind == LaunchKind.App)
        {
            settings.Usage[item.Id] = settings.Usage.GetValueOrDefault(item.Id) + 1;
            engine.SaveSoon();
        }
        try { item.Run(); }
        catch (Exception ex) { engine.ReportError($"Couldn't open \"{item.Title}\": {ex.Message}"); }
    }

    LaunchItem AppItem(AppEntry app) => new(AppKey(app), app.Name, "App", LaunchKind.App,
        () => Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{app.AppId}") { UseShellExecute = true }));

    static string AppKey(AppEntry app) => "app:" + app.AppId;

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    /// <summary>Exact &gt; starts with &gt; a word starts with &gt; initials ("vsc") &gt; contains &gt; letters in order.</summary>
    public static double Score(string name, string query)
    {
        var n = name.ToLowerInvariant();
        var q = query.ToLowerInvariant();
        if (n == q) return 1000;
        if (n.StartsWith(q)) return 800 - n.Length;
        var words = n.Split([' ', '-', '_', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q))) return 600 - n.Length;
        var initials = string.Concat(words.Select(w => w[0]));
        if (q.Length >= 2 && initials.StartsWith(q)) return 500;
        if (n.Contains(q)) return 300 - n.IndexOf(q);
        int at = 0;
        foreach (char c in n)
            if (at < q.Length && c == q[at]) at++;
        return at == q.Length && q.Length >= 3 ? 100 - n.Length : 0;
    }

    static bool TryCalculate(string query, out string answer)
    {
        answer = "";
        var expr = query.TrimStart('=').Replace('×', '*').Replace('÷', '/').Replace(',', '.');
        if (!MathRegex().IsMatch(expr) || !expr.Any(char.IsDigit) || !expr.Any(c => "+-*/%()".Contains(c))) return false;
        try
        {
            var value = Convert.ToDouble(new DataTable { Locale = CultureInfo.InvariantCulture }.Compute(expr, null), CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            answer = Math.Round(value, 10).ToString("0.##########", CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool LooksLikeUrl(string q) => !q.Contains(' ') && UrlRegex().IsMatch(q);

    static bool LooksLikePath(string q) =>
        q.Length > 2 && (q[1] == ':' || q.StartsWith(@"\\") || q.StartsWith('%')) && (File.Exists(Environment.ExpandEnvironmentVariables(q)) || Directory.Exists(Environment.ExpandEnvironmentVariables(q)));

    [GeneratedRegex(@"^[\d\s\.\+\-\*/%\(\)]+$")]
    private static partial Regex MathRegex();

    [GeneratedRegex(@"^(https?://)?[\w-]+(\.[\w-]+)+(:\d+)?(/\S*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
