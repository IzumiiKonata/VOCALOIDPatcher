using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VOCALOIDPatcher.Formats.Util;

public static class Texts
{
    private static readonly Regex UnsafeFileNameChars = new("[\\\\/:*?\"<>|]", RegexOptions.Compiled);

    public static string ReadText(byte[] bytes, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        return encoding.GetString(bytes);
    }

    public static string GetSafeFileName(string name) => UnsafeFileNameChars.Replace(name, "");

    public static Encoding ShiftJis()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }

    public static string DetectAndDecode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return ShiftJis().GetString(bytes);
        }
    }

    public static IReadOnlyList<string> LinesNotBlank(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
}
