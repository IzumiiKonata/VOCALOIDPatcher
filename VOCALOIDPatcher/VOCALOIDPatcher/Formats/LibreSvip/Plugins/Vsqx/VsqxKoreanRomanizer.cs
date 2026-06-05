using System.Collections.Generic;
using System.Text;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

internal static class VsqxKoreanRomanizer
{
    private const int SBase = 0xAC00;
    private const int LCount = 19;
    private const int VCount = 21;
    private const int TCount = 28;
    private const int NCount = VCount * TCount;
    private const int SCount = LCount * NCount;

    private static readonly string[] Initials =
    {
        "g", "gg", "n", "d", "dd", "r", "m", "b", "bb", "s",
        "ss", "", "j", "jj", "ch", "k", "t", "p", "h",
    };

    private static readonly string[] Vowels =
    {
        "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa",
        "wae", "oe", "yo", "u", "weo", "we", "wi", "yu", "eu", "eui", "i",
    };

    private static readonly string[] Finals =
    {
        "", "g", "gg", "gs", "n", "nch", "nh", "d", "l", "lg",
        "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "ps", "s",
        "ss", "ng", "j", "ch", "k", "t", "p", "h",
    };

    public static string Hangul2Xsampa(string lyric)
    {
        var phonemes = new List<string>();
        foreach (char ch in lyric)
        {
            int code = ch - SBase;
            if (code < 0 || code >= SCount)
                continue;
            int initialIndex = code / NCount;
            int vowelIndex = code % NCount / TCount;
            int finalIndex = code % TCount;

            string initialRomaji = Initials[initialIndex];
            string vowelRomaji = Vowels[vowelIndex];
            string finalRomaji = Finals[finalIndex];

            string initialXsampa = Lookup(initialRomaji);
            string vowelXsampa = Lookup(vowelRomaji);
            string finalXsampa = Lookup(finalRomaji);

            if ((initialXsampa == "s" || initialXsampa == "sh") && StartsWithAny(vowelXsampa, "i", "y", "j"))
                initialXsampa = initialXsampa == "s" ? "sh" : "sh'";
            else if ((vowelXsampa == "s" || vowelXsampa == "sh") && StartsWithAny(finalXsampa, "i", "y", "j"))
                vowelXsampa = vowelXsampa == "s" ? "sh" : "sh'";

            if (VsqxPhonemeMaps.Romaji2KoreanXsampaFinal.TryGetValue(finalXsampa, out var mappedFinal))
                finalXsampa = mappedFinal;

            string joined = $"{initialXsampa} {vowelXsampa} {finalXsampa}".Trim();
            phonemes.Add(joined);
        }
        return phonemes.Count > 0 ? string.Join(" ", phonemes) : "r a";
    }

    private static string Lookup(string romaji) =>
        VsqxPhonemeMaps.Romaji2KoreanXsampa.TryGetValue(romaji, out var value) ? value : "";

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (string prefix in prefixes)
            if (value.StartsWith(prefix))
                return true;
        return false;
    }
}
