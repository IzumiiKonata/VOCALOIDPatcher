using System.Globalization;

namespace VOCALOIDPatcher.Formats.Util;

public static class TextUtil
{
    public static string PadStartZero(this int value, int length) =>
        value.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');

    public static (string First, string Second) SplitFirst(this string text, string separator)
    {
        int index = text.IndexOf(separator, System.StringComparison.Ordinal);
        if (index < 0)
            return (text, "");
        return (text[..index], text[(index + separator.Length)..]);
    }
}
