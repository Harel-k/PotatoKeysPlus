using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PotatoKeysPlus.Models;

public static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PotatoKeysPlus");

    public static string FilePath { get; } = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch
        {
            // A broken settings file should never stop the app: keep a copy and start fresh.
            try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch { }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
