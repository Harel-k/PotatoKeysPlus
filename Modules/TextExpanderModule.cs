using System.Globalization;
using System.Text;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Interop;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Services;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Text Expander: type a short code like ;mail anywhere and it turns into the full text.
/// Only the last few typed characters are kept, in memory, to spot codes. Nothing is saved.
/// Placeholders: {date} {time} {clipboard}.
/// </summary>
public sealed class TextExpanderModule(ExpanderSettings settings, KeyboardHook hook)
{
    const int BufferSize = 40;
    readonly StringBuilder _typed = new();

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (!settings.Enabled) return false;

        // Moving the cursor or using shortcuts means the next letters start a new word.
        if ((mods & (Mods.Ctrl | Mods.Alt | Mods.Win)) != 0 || vk is 0x1B or 0x09 or (>= 0x21 and <= 0x28) or 0x2E)
        {
            _typed.Clear();
            return false;
        }
        if (vk == 0x08)
        {
            if (_typed.Length > 0) _typed.Length--;
            return false;
        }
        if (KeyNames.IsModifier(vk)) return false;

        var ch = Native.CharFor(vk, mods.HasFlag(Mods.Shift), hook.IsDown(0xA5));
        if (string.IsNullOrEmpty(ch) || char.IsControl(ch[0]))
        {
            _typed.Clear();
            return false;
        }

        _typed.Append(ch);
        if (_typed.Length > BufferSize) _typed.Remove(0, _typed.Length - BufferSize);

        var typed = _typed.ToString();
        var match = settings.Items
            .Where(e => e.Enabled && e.Abbreviation.Length > 0 && typed.EndsWith(e.Abbreviation, StringComparison.Ordinal))
            .OrderByDescending(e => e.Abbreviation.Length)
            .FirstOrDefault();
        if (match == null) return false;

        _typed.Clear();
        int erase = match.Abbreviation.Length - 1; // the last key is blocked, so it never reaches the app
        var text = match.Text;
        ClipboardHelper.Run(() =>
        {
            ClipboardHelper.WaitForModifiersReleased();
            for (int i = 0; i < erase; i++) InputSender.KeyTap(0x08);
            InputSender.TypeText(Fill(text));
        });
        return true;
    }

    static string Fill(string text)
    {
        if (text.Contains("{date}")) text = text.Replace("{date}", DateTime.Now.ToString("d", CultureInfo.CurrentCulture));
        if (text.Contains("{time}")) text = text.Replace("{time}", DateTime.Now.ToString("t", CultureInfo.CurrentCulture));
        if (text.Contains("{clipboard}"))
        {
            string clip = "";
            try { if (System.Windows.Clipboard.ContainsText()) clip = System.Windows.Clipboard.GetText(); } catch { }
            text = text.Replace("{clipboard}", clip);
        }
        return text;
    }
}
