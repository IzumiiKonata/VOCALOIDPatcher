using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;

public sealed class UstConverter : FormatConverter
{
    private const string LineSeparator = "\r\n";

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var text = TextHelper.DetectAndDecode(content);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var tempos = new List<SongTempo>();
        var notes = new List<Note>();
        int time = 0;
        double? headerTempo = null;

        string? sectionName = null;
        var section = new Dictionary<string, string>();

        void Flush()
        {
            if (sectionName == null)
                return;
            string upper = sectionName.ToUpperInvariant();
            if (upper.Contains("VERSION") || upper.Contains("TRACKEND"))
                return;
            if (upper.Contains("SETTING"))
            {
                if (section.TryGetValue("Tempo", out var t) && TryParseDouble(t, out var bpm))
                    headerTempo = bpm;
                return;
            }
            if (!section.TryGetValue("Length", out var lengthStr) || !TryParseDouble(lengthStr, out var lengthValue))
                return;
            int length = (int)Math.Round(lengthValue);
            if (section.TryGetValue("Tempo", out var noteTempoStr) && TryParseDouble(noteTempoStr, out var noteBpm))
                tempos.Add(new SongTempo(time, noteBpm));
            string lyric = section.TryGetValue("Lyric", out var ly) ? ly : "";
            int noteNum = section.TryGetValue("NoteNum", out var nn) && int.TryParse(nn, out var key) ? key : 60;
            if (!string.IsNullOrEmpty(lyric) && lyric.ToUpperInvariant() != "R")
                notes.Add(new Note { StartPos = time, Length = length, KeyNumber = noteNum, Lyric = lyric });
            time += length;
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("[#", StringComparison.Ordinal))
            {
                Flush();
                sectionName = line.Trim().TrimStart('[').TrimEnd(']');
                if (sectionName.StartsWith("#", StringComparison.Ordinal))
                    sectionName = sectionName[1..];
                section = new Dictionary<string, string>();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq > 0)
                section[line[..eq]] = line[(eq + 1)..];
        }
        Flush();

        if (headerTempo != null)
            tempos.Insert(0, new SongTempo(0, headerTempo.Value));
        if (tempos.Count == 0)
            tempos.Add(new SongTempo(0, Constants.DefaultBpm));

        var track = new SingingTrack { NoteList = notes };
        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = new List<TimeSignature> { new() },
            TrackList = new List<Track> { track },
        };
    }

    public override byte[] Dump(Project project)
    {
        var tempoList = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new(0, Constants.DefaultBpm) };
        var track = project.TrackList.OfType<SingingTrack>().FirstOrDefault(t => t.NoteList.Count > 0)
                    ?? project.TrackList.OfType<SingingTrack>().FirstOrDefault();
        if (track == null)
            throw new InvalidOperationException("No singing track found");

        var builder = new StringBuilder();
        void Append(string line) => builder.Append(line).Append(LineSeparator);

        double firstBpm = tempoList[0].Bpm;
        Append("[#VERSION]");
        Append("UST Version1.2");
        Append("[#SETTING]");
        Append("Tempo=" + ToFixed(firstBpm));
        Append("Tracks=1");
        Append("ProjectName=" + (string.IsNullOrEmpty(track.Title) ? "Untitled" : track.Title));
        Append("Mode2=True");

        double prevBpm = firstBpm;
        int prevEnd = 0;
        int noteIndex = 0;
        foreach (var note in track.NoteList)
        {
            int restLength = note.StartPos - prevEnd;
            if (restLength > 0)
            {
                Append("[#" + Pad(noteIndex) + "]");
                Append("Length=" + restLength);
                Append("Lyric=R");
                Append("NoteNum=60");
                Append("PreUtterance=");
                noteIndex++;
            }
            double curBpm = BpmForPosition(tempoList, note.StartPos);
            Append("[#" + Pad(noteIndex) + "]");
            Append("Length=" + note.Length);
            Append("Lyric=" + note.Lyric);
            Append("NoteNum=" + note.KeyNumber);
            if (curBpm != prevBpm)
            {
                Append("Tempo=" + ToFixed(curBpm));
                prevBpm = curBpm;
            }
            Append("PreUtterance=");
            prevEnd = note.EndPos;
            noteIndex++;
        }
        Append("[#TRACKEND]");

        return TextHelper.ShiftJis().GetBytes(builder.ToString());
    }

    private static double BpmForPosition(List<SongTempo> tempoList, int position)
    {
        double bpm = tempoList[0].Bpm;
        foreach (var tempo in tempoList)
        {
            if (tempo.Position <= position)
                bpm = tempo.Bpm;
            else
                break;
        }
        return bpm;
    }

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string ToFixed(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string Pad(int value) => value.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
}
