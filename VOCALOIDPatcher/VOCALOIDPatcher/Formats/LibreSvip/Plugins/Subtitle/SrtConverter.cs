using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;

public sealed class SrtConverter : FormatConverter
{
    public LyricSplitMode SplitBy { get; set; } = LyricSplitMode.Both;
    public bool IgnoreSlurNotes { get; set; } = true;
    public int Offset { get; set; }
    public int TrackIndex { get; set; } = -1;
    public string Encoding { get; set; } = "utf-8";

    public override bool CanDump => true;

    public override byte[] Dump(Project project)
    {
        var tempoList = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new(0, Constants.DefaultBpm) };
        var synchronizer = new TimeSynchronizer(tempoList);
        var singingTrack = TrackIndex >= 0 && TrackIndex < project.TrackList.Count
            ? project.TrackList[TrackIndex] as SingingTrack
            : project.TrackList.OfType<SingingTrack>().FirstOrDefault(t => t.NoteList.Count > 0);
        if (singingTrack == null)
            throw new InvalidOperationException("No singing track found");

        var builder = new StringBuilder();
        int index = 1;
        foreach (var buffer in SubtitleSupport.SplitLines(singingTrack.NoteList, SplitBy))
        {
            int startMs = (int)(synchronizer.GetActualSecsFromTicks(buffer[0].StartPos) * 1000) - Offset;
            int endMs = (int)(synchronizer.GetActualSecsFromTicks(buffer[^1].EndPos) * 1000) - Offset;
            string text = SubtitleSupport.BuildText(buffer, IgnoreSlurNotes).Trim();
            builder.Append(index).Append('\n');
            builder.Append(FormatTime(startMs)).Append(" --> ").Append(FormatTime(endMs)).Append('\n');
            builder.Append(text).Append('\n').Append('\n');
            index++;
        }
        return TextHelper.GetEncoding(Encoding).GetBytes(builder.ToString());
    }

    private static string FormatTime(int totalMs)
    {
        if (totalMs < 0)
            totalMs = 0;
        int ms = totalMs % 1000;
        int totalSeconds = totalMs / 1000;
        int second = totalSeconds % 60;
        int minute = totalSeconds / 60 % 60;
        int hour = totalSeconds / 3600;
        return $"{hour:00}:{minute:00}:{second:00},{ms:000}";
    }
}
