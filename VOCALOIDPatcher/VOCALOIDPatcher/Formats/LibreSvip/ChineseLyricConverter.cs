using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToolGood.Words.Pinyin;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

namespace VOCALOIDPatcher.Formats.LibreSvip;

internal static class ChineseLyricConverter
{
    private const char Separator = '\u001f';

    public static bool LooksLikeChinese(IReadOnlyList<Note> notes)
    {
        bool hasChinese = false;
        foreach (var note in notes)
        {
            foreach (char character in note.Lyric)
            {
                if (IsKana(character))
                    return false;
                if (IsChinese(character))
                    hasChinese = true;
            }
        }
        return hasChinese;
    }

    public static IReadOnlyList<string> Convert(IReadOnlyList<Note> notes)
    {
        var result = notes.Select(note => note.Lyric).ToArray();
        int index = 0;

        while (index < notes.Count)
        {
            while (index < notes.Count && CountChinese(notes[index].Lyric) == 0)
                index++;

            if (index >= notes.Count)
                break;

            int end = index;
            var text = new StringBuilder();

            while (end < notes.Count)
            {
                var note = notes[end];
                int count = AppendChinese(text, note.Lyric);
                if (count == 0)
                    break;

                bool boundary = HasPhraseBoundary(note.Lyric);
                end++;

                if (boundary || end >= notes.Count)
                    break;

                var next = notes[end];
                if (CountChinese(next.Lyric) == 0 || next.StartPos - note.EndPos > Core.Constants.TicksInBeat)
                    break;
            }

            ApplyChunk(notes, result, index, end, text.ToString());
            index = end;
        }

        return result;
    }

    private static void ApplyChunk(IReadOnlyList<Note> notes, string[] result, int start, int end, string text)
    {
        var syllables = WordsHelper.GetPinyin(text, Separator.ToString())
            .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .ToArray();

        int expected = 0;
        for (int i = start; i < end; i++)
            expected += CountChinese(notes[i].Lyric);

        if (syllables.Length != expected || syllables.Any(string.IsNullOrEmpty))
            syllables = text.Select(character => Normalize(WordsHelper.GetPinyin(character.ToString()))).ToArray();

        int syllableIndex = 0;
        for (int i = start; i < end; i++)
        {
            int count = CountChinese(notes[i].Lyric);
            if (TryUsePronunciation(notes[i].Pronunciation, count, out var pronunciation))
            {
                result[i] = pronunciation;
                syllableIndex += count;
                continue;
            }

            var converted = RebuildLyric(notes[i].Lyric, syllables, ref syllableIndex);
            if (!string.IsNullOrEmpty(converted))
                result[i] = converted;
        }
    }

    private static string RebuildLyric(string lyric, IReadOnlyList<string> syllables, ref int syllableIndex)
    {
        var parts = new List<string>();
        var latin = new StringBuilder();

        void FlushLatin()
        {
            if (latin.Length == 0)
                return;
            parts.Add(latin.ToString().ToLowerInvariant());
            latin.Clear();
        }

        foreach (char character in lyric)
        {
            if (IsChinese(character))
            {
                FlushLatin();
                if (syllableIndex < syllables.Count)
                    parts.Add(syllables[syllableIndex]);
                syllableIndex++;
            }
            else if (IsLatin(character))
            {
                latin.Append(character);
            }
            else
            {
                FlushLatin();
            }
        }

        FlushLatin();
        return string.Join(" ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private static bool TryUsePronunciation(string? value, int expectedCount, out string pronunciation)
    {
        pronunciation = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .ToArray();
        if (parts.Length != expectedCount || parts.Any(part => !VsqxPhonemeMaps.Pinyin2Xsampa.ContainsKey(part)))
            return false;

        pronunciation = string.Join(" ", parts);
        return true;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace("u:", "v").Replace('ü', 'v');

    private static int CountChinese(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int count = 0;
        foreach (char character in value)
        {
            if (IsChinese(character))
                count++;
        }
        return count;
    }

    private static int AppendChinese(StringBuilder target, string? value)
    {
        int before = target.Length;
        if (!string.IsNullOrEmpty(value))
        {
            foreach (char character in value)
            {
                if (IsChinese(character))
                    target.Append(character);
            }
        }
        return target.Length - before;
    }

    private static bool IsChinese(char value) =>
        value is >= '\u3400' and <= '\u4db5' or >= '\u4e00' and <= '\u9fa5';

    private static bool IsLatin(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsKana(char value) =>
        value is >= '\u3040' and <= '\u30ff' or >= '\uff66' and <= '\uff9d';

    private static bool HasPhraseBoundary(string? value) =>
        !string.IsNullOrEmpty(value) && value.IndexOfAny(new[] { '，', '。', '！', '？', '；', '：', ',', '.', '!', '?', ';', ':' }) >= 0;
}
