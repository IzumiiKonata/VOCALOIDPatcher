using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Lrc;

public enum LrcOffsetPolicy
{
    Timeline,
    Meta,
}

public sealed class LrcConverter : FormatConverter
{
    public string Artist { get; set; } = "";
    public string Title { get; set; } = "";
    public string Album { get; set; } = "";
    public string By { get; set; } = "";
    public LyricSplitMode SplitBy { get; set; } = LyricSplitMode.Both;
    public bool IgnoreSlurNotes { get; set; } = true;
    public int Offset { get; set; }
    public LrcOffsetPolicy OffsetPolicy { get; set; } = LrcOffsetPolicy.Timeline;
    public bool Timeline { get; set; } = true;
    public string Encoding { get; set; } = "utf-8";

    public override bool CanDump => true;

    public override byte[] Dump(Project project)
    {
        var tempoList = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new(0, Constants.DefaultBpm) };
        var synchronizer = new TimeSynchronizer(tempoList);
        var singingTrack = project.TrackList.OfType<SingingTrack>().FirstOrDefault()
            ?? throw new InvalidOperationException("No singing track found");

        var builder = new StringBuilder();
        AppendInfo(builder, "ti", Title);
        AppendInfo(builder, "ar", Artist);
        AppendInfo(builder, "al", Album);
        AppendInfo(builder, "by", By);
        if (OffsetPolicy == LrcOffsetPolicy.Meta && Offset != 0)
            AppendInfo(builder, "offset", Offset.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (var buffer in SubtitleSupport.SplitLines(singingTrack.NoteList, SplitBy))
        {
            string lyric = SubtitleSupport.BuildText(buffer, IgnoreSlurNotes);
            if (Timeline)
            {
                int offset = OffsetPolicy == LrcOffsetPolicy.Timeline ? Offset : 0;
                builder.Append('[').Append(FormatTime(buffer[0].StartPos, synchronizer, offset)).Append(']');
            }
            builder.Append(lyric).Append('\n');
        }

        return TextHelper.GetEncoding(Encoding).GetBytes(builder.ToString());
    }

    private static string FormatTime(int ticks, TimeSynchronizer synchronizer, int offset)
    {
        double secs = synchronizer.GetActualSecsFromTicks(ticks);
        long totalMs = Math.Max(0, (long)Math.Round(secs * 1000) - offset);
        long minute = totalMs / 60000;
        long second = totalMs / 1000 % 60;
        long milli = totalMs % 1000;
        return $"{minute:00}:{second:00}.{milli:000}";
    }

    private static void AppendInfo(StringBuilder builder, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            builder.Append('[').Append(key).Append(':').Append(value).Append("]\n");
    }
}
