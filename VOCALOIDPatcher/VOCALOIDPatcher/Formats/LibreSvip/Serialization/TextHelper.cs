using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Serialization;

public static class TextHelper
{
    private static Encoding? _shiftJis;

    public static Encoding ShiftJis()
    {
        if (_shiftJis == null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _shiftJis = Encoding.GetEncoding(932);
        }
        return _shiftJis;
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

    public static byte[] EncodeUtf8(string text) => new UTF8Encoding(false).GetBytes(text);
}
