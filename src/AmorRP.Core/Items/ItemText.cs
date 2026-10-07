using System.Globalization;
using System.Text;

namespace AmorRP.Core.Items;

public static class ItemText
{
    public static bool Plain(string? text, int maximum, bool multiline = false, bool required = true)
    {
        if (text == null || (required && string.IsNullOrWhiteSpace(text)) || new StringInfo(text).LengthInTextElements > maximum) return false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (multiline && rune.Value == '\n') continue;
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate) return false;
        }
        return true;
    }
    public static bool Chat(string? text, int maximum = 50) => Plain(text, maximum)
        && Encoding.UTF8.GetByteCount(text!) <= 200 && !text!.Contains('<') && !text.Contains('>');
    public static bool Destination(string text) => text is "emote" or "say" or "party"
        || Enumerable.Range(1, 8).Any(n => text == "linkshell:" + n || text == "crossworld-linkshell:" + n);
}
