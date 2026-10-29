using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PotatoKeysPlus.Services;

public sealed record UpdateInfo(Version Version, string Tag, string AssetName, string DownloadUrl, long Size, string? Sha256, string Notes, string PageUrl);

/// <summary>
/// Checks GitHub Releases for a newer version, downloads its installer and runs it silently.
/// A release needs an asset named like "PotatoKeysPlus-Setup-x.y.z.exe".
/// </summary>
public static class Updater
{
    public const string Owner = "Harel-k";
    public const string Repo = "PotatoKeysPlus";
    public static string RepoUrl => $"https://github.com/{Owner}/{Repo}";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PotatoKeysPlus/{CurrentVersion}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static Version CurrentVersion => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>Returns the newer release, or null when you're up to date (or no release exists yet).</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        version = Normalize(version);
        if (version <= CurrentVersion) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                continue;

            string? sha = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } d && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha = d["sha256:".Length..];

            return new UpdateInfo(
                version, tag, name,
                asset.GetProperty("browser_download_url").GetString()!,
                asset.GetProperty("size").GetInt64(),
                sha,
                root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                root.GetProperty("html_url").GetString() ?? RepoUrl);
        }
        return null;
    }

    /// <summary>Downloads the installer to %TEMP% and checks its SHA-256 when GitHub provides one.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct = default)
    {
        var folder = Path.Combine(Path.GetTempPath(), "PotatoKeysPlus-update");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(update.AssetName));

        using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? update.Size;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(path);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                hash.AppendData(buffer, 0, read);
                done += read;
                if (total > 0) progress?.Report((double)done / total);
            }

            var actual = Convert.ToHexString(hash.GetHashAndReset());
            if (update.Sha256 != null && !actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                output.Close();
                File.Delete(path);
                throw new InvalidDataException("The downloaded update didn't match its checksum, so it wasn't installed. Try again later.");
            }
        }
        return path;
    }

    /// <summary>Runs the installer silently. It closes this app, updates the files and starts the new version.</summary>
    public static void LaunchInstaller(string installerPath) =>
        Process.Start(new ProcessStartInfo(installerPath, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });
}
