using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Services;

/// <summary>
/// Settings Backup: everything (settings, hotkeys, snippets, notes) in one .potatokeys file
/// that can be restored here or on another PC.
/// </summary>
public static class Backup
{
    public const string Extension = ".potatokeys";

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static void Export(AppSettings settings, string notesFolder, string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(zip, "settings.json", JsonSerializer.Serialize(settings, Options));
        Write(zip, "about.txt", $"Potato Keys+ backup\nApp version: {Updater.CurrentVersion}\nMade: {DateTime.Now:u}\n");
        if (Directory.Exists(notesFolder))
            foreach (var note in Directory.GetFiles(notesFolder, "*.txt"))
                zip.CreateEntryFromFile(note, "notes/" + Path.GetFileName(note));
    }

    /// <summary>Checks the file and returns a short summary, or throws with a friendly message.</summary>
    public static string Describe(string path)
    {
        var settings = ReadSettings(path, out int notes);
        return $"{settings.Hotkeys.Items.Count} hotkeys, {settings.Snippets.Items.Count} wheel items, " +
               $"{settings.Expander.Items.Count} text expansions and {notes} notes";
    }

    /// <summary>Replaces the current settings and notes with the backup's.</summary>
    public static void Restore(string path, string notesFolder)
    {
        var settings = ReadSettings(path, out _);
        using var zip = ZipFile.OpenRead(path);
        SettingsStore.Save(settings);
        Directory.CreateDirectory(notesFolder);
        foreach (var old in Directory.GetFiles(notesFolder, "*.txt")) File.Delete(old);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("notes/") && e.Name.EndsWith(".txt")))
            entry.ExtractToFile(Path.Combine(notesFolder, Path.GetFileName(entry.Name)), overwrite: true);
    }

    static AppSettings ReadSettings(string path, out int notes)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("settings.json") ?? throw new InvalidDataException();
            using var reader = new StreamReader(entry.Open());
            var settings = JsonSerializer.Deserialize<AppSettings>(reader.ReadToEnd(), Options) ?? throw new InvalidDataException();
            notes = zip.Entries.Count(e => e.FullName.StartsWith("notes/") && e.Name.EndsWith(".txt"));
            return settings;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("That file isn't a Potato Keys+ backup, or it's damaged.");
        }
    }

    static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }
}
