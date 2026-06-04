using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;

public enum LyricSplitMode { Both, Gap, Symbol }

public static class SubtitleSupport
{
    public static readonly Regex LatinAlphabet = new("[a-zA-Z]+", RegexOptions.Compiled);

    public static readonly Regex SymbolPattern = new(
        @"(?!-)[!""#$%&'()*,./:;<=>?\[\\\]^_`{|}~。，、；：？！…—－～（）《》「」『』【】〔〕〈〉·　]+",
        RegexOptions.Compiled);

    public static List<List<Note>> SplitLines(IReadOnlyList<Note> noteList, LyricSplitMode mode)
    {
        var result = new List<List<Note>>();
        var buffer = new List<Note>();
        for (int i = 0; i < noteList.Count; i++)
        {
            var note = noteList[i];
            buffer.Add(note);
            bool conditionSymbol = SymbolPattern.IsMatch(note.Lyric);
            bool conditionGap = i + 1 < noteList.Count && noteList[i + 1].StartPos - note.EndPos >= 60;
            bool commit = mode switch
            {
                LyricSplitMode.Symbol => conditionSymbol,
                LyricSplitMode.Gap => conditionGap,
                _ => conditionSymbol || conditionGap,
            };
            if (i + 1 == noteList.Count)
                commit = true;
            if (commit)
            {
                result.Add(buffer);
                buffer = new List<Note>();
            }
        }
        return result;
    }

    public static string BuildText(List<Note> buffer, bool ignoreSlurNotes)
    {
        var text = new StringBuilder();
        foreach (var note in buffer)
        {
            if (ignoreSlurNotes && note.Lyric == "-")
                continue;
            text.Append(SymbolPattern.Replace(note.Lyric, ""));
            if (LatinAlphabet.IsMatch(note.Lyric))
                text.Append(' ');
        }
        return text.ToString();
    }
}
