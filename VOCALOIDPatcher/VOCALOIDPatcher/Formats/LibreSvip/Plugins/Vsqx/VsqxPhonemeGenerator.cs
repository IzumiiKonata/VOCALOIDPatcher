using System;
using System.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

internal static class VsqxPhonemeGenerator
{
    private const string DefaultChinesePhoneme = "l a";
    private const string DefaultJapanesePhoneme = "4 a";

    public static (string Lyric, string Phoneme) Generate(string lyric, VocaloidLanguage language)
    {
        if (VsqxPhonemeMaps.LegatoChars.Contains(lyric))
            return (lyric, "-");

        switch (language)
        {
            case VocaloidLanguage.SimplifiedChinese:
            {
                string key = lyric;
                string phoneme = VsqxPhonemeMaps.Pinyin2Xsampa.TryGetValue(key, out var cn)
                    ? cn
                    : DefaultChinesePhoneme;
                return (key, phoneme);
            }
            case VocaloidLanguage.Japanese:
            {
                string phoneme = VsqxPhonemeMaps.Romaji2Xsampa.TryGetValue(lyric, out var jp)
                    ? jp
                    : DefaultJapanesePhoneme;
                return (lyric, phoneme);
            }
            case VocaloidLanguage.Korean:
                return (lyric, VsqxKoreanRomanizer.Hangul2Xsampa(lyric));
            default:
                return (lyric, DefaultChinesePhoneme);
        }
    }
}
