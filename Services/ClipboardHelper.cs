using System.Windows;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;

namespace PotatoKeysPlus.Services;

/// <summary>
/// Reads the selected text in any app (by pressing Ctrl+C for you) and pastes text back, keeping
/// whatever was on your clipboard before. Runs on its own STA thread so the keyboard never lags.
/// </summary>
public static class ClipboardHelper
{
    static readonly string[] KeepFormats =
        [DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.Bitmap, DataFormats.FileDrop];

    public static void Run(Action body)
    {
        var thread = new Thread(() =>
        {
            try { body(); } catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    public static void WaitForModifiersReleased()
    {
        var until = DateTime.Now.AddMilliseconds(1500);
        while (DateTime.Now < until)
        {
            if (new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.All(vk => (Native.GetAsyncKeyState(vk) & 0x8000) == 0)) return;
            Thread.Sleep(10);
        }
    }

    /// <summary>Copies the current selection and returns it (null when nothing is selected). Call on an STA thread.</summary>
    public static string? CopySelection()
    {
        WaitForModifiersReleased();
        Retry(Clipboard.Clear);
        InputSender.Combo(new KeyBind(0x43, Mods.Ctrl));
        var until = DateTime.Now.AddMilliseconds(700);
        while (DateTime.Now < until)
        {
            Thread.Sleep(30);
            if (Retry(Clipboard.ContainsText)) return Retry(Clipboard.GetText);
        }
        return null;
    }

    public static void Paste(string text)
    {
        Retry(() => Clipboard.SetText(text));
        Thread.Sleep(30);
        InputSender.Combo(new KeyBind(0x56, Mods.Ctrl));
        Thread.Sleep(250); // let the app read the clipboard before we put the old contents back
    }

    public static DataObject? Save()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current == null) return null;
            var copy = new DataObject();
            foreach (var format in KeepFormats)
                if (current.GetDataPresent(format) && current.GetData(format) is { } data) copy.SetData(format, data);
            return copy;
        }
        catch
        {
            return null;
        }
    }

    public static void Restore(DataObject? saved)
    {
        if (saved == null || saved.GetFormats().Length == 0) return;
        Retry(() => Clipboard.SetDataObject(saved, true));
    }

    // The clipboard is shared by every app, so it's briefly "busy" now and then.
    static T Retry<T>(Func<T> f)
    {
        for (int i = 0; ; i++)
        {
            try { return f(); }
            catch when (i < 8) { Thread.Sleep(25); }
        }
    }

    static void Retry(Action a) => Retry(() => { a(); return true; });
}
