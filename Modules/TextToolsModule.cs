using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using PotatoKeysPlus.Input;
using PotatoKeysPlus.Models;
using PotatoKeysPlus.Services;

namespace PotatoKeysPlus.Modules;

/// <summary>
/// Text Tools: select text in any app, press the hotkey, pick a tool (1-8) and the text is replaced.
/// Also a "paste as plain text" hotkey that strips colours, fonts and links.
/// </summary>
public sealed partial class TextToolsModule(TextToolsSettings settings, Engine engine)
{
    public interface IMenuView
    {
        void Open(IReadOnlyList<string> items);
        void Select(int index);
        void Close();
    }

    public static readonly (string Name, Func<string, string>? Transform)[] Tools =
    [
        ("UPPERCASE", s => s.ToUpper(CultureInfo.CurrentCulture)),
        ("lowercase", s => s.ToLower(CultureInfo.CurrentCulture)),
        ("Title Case", s => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower(CultureInfo.CurrentCulture))),
        ("Sentence case", SentenceCase),
        ("Remove extra spaces", s => Spaces().Replace(s.Trim(), " ")),
        ("Join lines into one", s => Spaces().Replace(LineBreaks().Replace(s, " "), " ").Trim()),
        ("Sort lines A→Z", s => string.Join(Environment.NewLine, s.Split(["\r\n", "\n"], StringSplitOptions.None).Order(StringComparer.CurrentCultureIgnoreCase))),
        ("Count words", null),
    ];

    int _selected = -1;

    public IMenuView? View { get; set; }
    public bool IsOpen { get; private set; }

    public bool OnKeyDown(int vk, Mods mods)
    {
        if (IsOpen)
        {
            int digit = vk is >= 0x31 and <= 0x38 ? vk - 0x31 : vk is >= 0x61 and <= 0x68 ? vk - 0x61 : -1;
            if (digit >= 0 && digit < Tools.Length) { Run(digit); return true; }
            switch (vk)
            {
                case 0x28: Select((_selected + 1) % Tools.Length); return true;
                case 0x26: Select(_selected <= 0 ? Tools.Length - 1 : _selected - 1); return true;
                case 0x0D: if (_selected >= 0) Run(_selected); return true;
                case 0x1B: Close(); return true;
            }
            if (KeyNames.IsModifier(vk)) return false;
            Close();
            return false;
        }

        if (!settings.Enabled) return false;
        if (settings.Menu.Matches(vk, mods))
        {
            IsOpen = true;
            _selected = -1;
            View?.Open(Tools.Select((t, i) => t.Name).ToList());
            return true;
        }
        if (settings.PlainPaste.Matches(vk, mods))
        {
            PastePlain();
            return true;
        }
        return false;
    }

    public void Select(int index)
    {
        _selected = index;
        View?.Select(index);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        View?.Close();
    }

    public void Run(int index)
    {
        Close();
        var (name, transform) = Tools[index];
        ClipboardHelper.Run(() =>
        {
            var saved = ClipboardHelper.Save();
            var text = ClipboardHelper.CopySelection();
            if (string.IsNullOrEmpty(text))
            {
                ClipboardHelper.Restore(saved);
                engine.Post(() => engine.ShowToast("Select some text first"));
                return;
            }
            if (transform == null)
            {
                ClipboardHelper.Restore(saved);
                int words = Words().Matches(text).Count;
                engine.Post(() => engine.ShowToast($"{words:N0} words  ·  {text.Length:N0} characters"));
                return;
            }
            ClipboardHelper.Paste(transform(text));
            ClipboardHelper.Restore(saved);
            engine.Post(() => engine.ShowToast(name));
        });
    }

    void PastePlain()
    {
        ClipboardHelper.Run(() =>
        {
            ClipboardHelper.WaitForModifiersReleased();
            if (!Clipboard.ContainsText()) return;
            var saved = ClipboardHelper.Save();
            ClipboardHelper.Paste(Clipboard.GetText());
            ClipboardHelper.Restore(saved);
        });
    }

    static string SentenceCase(string s)
    {
        var lower = s.ToLower(CultureInfo.CurrentCulture).ToCharArray();
        bool start = true;
        for (int i = 0; i < lower.Length; i++)
        {
            if (start && char.IsLetter(lower[i])) { lower[i] = char.ToUpper(lower[i], CultureInfo.CurrentCulture); start = false; }
            else if (lower[i] is '.' or '!' or '?' or '\n') start = true;
        }
        return new string(lower);
    }

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"\S+")]
    private static partial Regex Words();
}
