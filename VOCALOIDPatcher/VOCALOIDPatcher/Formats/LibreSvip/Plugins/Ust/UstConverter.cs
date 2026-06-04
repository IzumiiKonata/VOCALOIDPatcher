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

    public bool ImportPitch { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var text = TextHelper.DetectAndDecode(content);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var tempos = new List<SongTempo>();
        var notes = new List<Note>();
        var mode2Data = new List<UtauMode2NotePitchData?>();
        var mode1Data = new List<List<int>?>();
        int time = 0;
        double? headerTempo = null;
        bool? mode2Flag = null;
        bool sawPbs = false;

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
                if (section.TryGetValue("Mode2", out var m2))
                    mode2Flag = m2.Trim() is "True" or "1";
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
            {
                notes.Add(new Note { StartPos = time, Length = length, KeyNumber = noteNum, Lyric = lyric });
                mode2Data.Add(ParseMode2(section, ref sawPbs));
                mode1Data.Add(ParseMode1(section));
            }
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

        var timeSignatures = new List<TimeSignature> { new() };
        var track = new SingingTrack { NoteList = notes };
        if (ImportPitch && notes.Count > 0)
        {
            var synchronizer = new TimeSynchronizer(tempos);
            bool useMode2 = mode2Flag ?? sawPbs;
            track.EditedParams.Pitch = useMode2
                ? UstPitch.PitchFromMode2(mode2Data, synchronizer, notes, timeSignatures)
                : UstPitch.PitchFromMode1(mode1Data, synchronizer, notes, timeSignatures);
        }

        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = timeSignatures,
            TrackList = new List<Track> { track },
        };
    }

    private static UtauMode2NotePitchData? ParseMode2(Dictionary<string, string> section, ref bool sawPbs)
    {
        if (!section.TryGetValue("PBS", out var pbsStr))
            return null;
        sawPbs = true;
        var pbs = pbsStr.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => TryParseDouble(s, out var v) ? v : 0.0).ToList();
        return new UtauMode2NotePitchData
        {
            Start = pbs.Count > 0 ? pbs[0] : 0,
            StartShift = pbs.Count > 1 ? pbs[1] : null,
            Widths = ParseFloatList(section, "PBW"),
            Shifts = ParseFloatList(section, "PBY"),
            CurveTypes = section.TryGetValue("PBM", out var pbm)
                ? pbm.Split(',').Select(s => s.Trim()).ToList()
                : new List<string>(),
            VibratoParams = ParseVibrato(section),
        };
    }

    private static List<int>? ParseMode1(Dictionary<string, string> section)
    {
        foreach (var key in new[] { "Pitches", "Piches", "PitchBend" })
            if (section.TryGetValue(key, out var v))
                return v.Split(',').Select(s => int.TryParse(s.Trim(), out var n) ? n : 0).ToList();
        return null;
    }

    private static UtauNoteVibrato? ParseVibrato(Dictionary<string, string> section)
    {
        if (!section.TryGetValue("VBR", out var vbr))
            return null;
        var v = vbr.Split(',').Select(s => TryParseDouble(s, out var d) ? d : 0.0).ToList();
        if (v.Count == 0)
            return null;
        double At(int i) => i < v.Count ? v[i] : 0;
        return new UtauNoteVibrato
        {
            Length = At(0), Period = At(1), Depth = At(2),
            FadeIn = At(3), FadeOut = At(4), PhaseShift = At(5), Shift = At(6),
        };
    }

    private static List<double> ParseFloatList(Dictionary<string, string> section, string key) =>
        section.TryGetValue(key, out var s)
            ? s.Split(',').Select(x => TryParseDouble(x, out var v) ? v : 0.0).ToList()
            : new List<double>();

    public override byte[] Dump(Project project)
    {
        var tempoList = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new(0, Constants.DefaultBpm) };
        var track = project.TrackList.OfType<SingingTrack>().FirstOrDefault(t => t.NoteList.Count > 0)
                    ?? project.TrackList.OfType<SingingTrack>().FirstOrDefault();
        if (track == null)
            throw new InvalidOperationException("No singing track found");

        List<UtauMode2NotePitchData?> pitchData = track.EditedParams.Pitch.Points.Count > 0
            ? UstPitch.PitchToMode2(track.EditedParams.Pitch, track.NoteList, tempoList)
            : new List<UtauMode2NotePitchData?>();

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
        for (int i = 0; i < track.NoteList.Count; i++)
        {
            var note = track.NoteList[i];
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
            var pitch = i < pitchData.Count ? pitchData[i] : null;
            if (pitch != null && pitch.Start != null)
            {
                string pbs = ToFixed(pitch.Start.Value);
                if (pitch.StartShift != null)
                    pbs += ";" + ToFixed(pitch.StartShift.Value);
                Append("PBS=" + pbs);
                if (pitch.Widths.Count > 0)
                    Append("PBW=" + string.Join(",", pitch.Widths.Select(w => ToFixed(w))));
                if (pitch.Shifts.Count > 0)
                    Append("PBY=" + string.Join(",", pitch.Shifts.Select(s => ToFixed(s))));
            }
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
