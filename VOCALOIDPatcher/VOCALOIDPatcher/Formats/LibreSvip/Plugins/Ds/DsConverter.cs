using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ds;

public sealed class DsConverter : FormatConverter
{
    private static readonly Regex CentsRe = new(@"[+-]\d+$", RegexOptions.Compiled);

    public bool ImportPitch { get; set; } = true;
    public double Tempo { get; set; } = Constants.DefaultBpm;
    public int TrackIndex { get; set; } = -1;
    public string DictName { get; set; } = "opencpop-extension";
    public double SplitThreshold { get; set; } = 5;
    public int MinInterval { get; set; } = 400;
    public int Seed { get; set; } = -1;
    public bool ExportGender { get; set; }
    public int Indent { get; set; } = 2;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var items = JsonSerializer.Deserialize<List<DsItem>>(TextHelper.DetectAndDecode(content), JsonHelper.Default)
            ?? new List<DsItem>();
        var songTempoList = new List<SongTempo> { new(0, Tempo) };
        _synchronizer = new TimeSynchronizer(songTempoList);
        var track = new SingingTrack
        {
            NoteList = ParseNotes(items),
            EditedParams = new Params { Pitch = ParsePitch(items) },
        };
        return new Project
        {
            SongTempoList = songTempoList,
            TimeSignatureList = new List<TimeSignature> { new(0, 4, 4) },
            TrackList = new List<Track> { track },
        };
    }

    private List<Note> ParseNotes(List<DsItem> items)
    {
        var allNotes = new List<Note>();
        foreach (var item in items)
        {
            if (item.NoteDur == null || item.NoteSlur == null)
                continue;
            double curSecs = item.Offset;
            bool prevIsBreath = false;
            int lyricIndex = 0;
            var indexed = new List<(int Index, int Slur)>();
            for (int i = 0; i < item.NoteSlur.Count; i++)
                indexed.Add((i, item.NoteSlur[i]));
            foreach (var group in IterTools.SplitBefore(indexed, pair => pair.Slur == 0))
            {
                foreach (var (noteIndex, isSlur) in group)
                {
                    if (lyricIndex >= item.Text.Count || noteIndex >= item.NoteDur.Count || noteIndex >= item.NoteSeq.Count)
                        continue;
                    string text = item.Text[lyricIndex];
                    double noteDur = item.NoteDur[noteIndex];
                    string note = item.NoteSeq[noteIndex];
                    double curTime = _synchronizer.GetActualTicksFromSecs(curSecs);
                    double nextTime = _synchronizer.GetActualTicksFromSecs(curSecs + noteDur);
                    if (text == "SP")
                    {
                    }
                    else if (text == "AP")
                    {
                        prevIsBreath = true;
                    }
                    else
                    {
                        int midiKey = MusicMath.Note2Midi(CentsRe.Replace(note, ""));
                        if (isSlur == 0)
                        {
                            allNotes.Add(new Note
                            {
                                StartPos = (int)curTime,
                                Length = (int)nextTime - (int)curTime,
                                KeyNumber = midiKey,
                                Lyric = text,
                                HeadTag = prevIsBreath ? "V" : null,
                            });
                            prevIsBreath = false;
                        }
                        else
                        {
                            allNotes.Add(new Note
                            {
                                StartPos = (int)curTime,
                                Length = (int)nextTime - (int)curTime,
                                KeyNumber = midiKey,
                                Lyric = "-",
                            });
                        }
                    }
                    curSecs += noteDur;
                }
                lyricIndex++;
            }
        }
        return allNotes;
    }

    private ParamCurve ParsePitch(List<DsItem> items)
    {
        var points = new List<Point> { Point.StartPoint() };
        if (ImportPitch)
        {
            foreach (var item in items)
            {
                if (item.F0Timestep == null || item.F0Seq == null)
                    continue;
                double timestep = item.F0Timestep.Value;
                points.Add(new Point((int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset)) + 1920, -100));
                for (int i = 0; i < item.F0Seq.Count; i++)
                    points.Add(new Point(
                        (int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset + timestep * i)) + 1920,
                        (int)Math.Round(MusicMath.Hz2Midi(item.F0Seq[i]) * 100)));
                points.Add(new Point(
                    (int)Math.Round(_synchronizer.GetActualTicksFromSecs(item.Offset + timestep * (item.F0Seq.Count - 1))) + 1920,
                    -100));
            }
        }
        points.Add(Point.EndPoint());
        return new ParamCurve { Points = points };
    }

    public override byte[] Dump(Project project)
    {
        var singing = TrackIndex >= 0 && TrackIndex < project.TrackList.Count
            ? project.TrackList[TrackIndex] as SingingTrack
            : project.TrackList.OfType<SingingTrack>().FirstOrDefault(track => track.NoteList.Count > 0);
        if (singing == null)
            throw new InvalidOperationException("No singing track found");
        var tempos = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new() };
        var synchronizer = new TimeSynchronizer(tempos);
        var groups = SplitNotes(singing.NoteList, synchronizer);
        var items = groups.Select(group => GenerateItem(
            group, singing, synchronizer, project.TimeSignatureList)).ToList();
        var options = new JsonSerializerOptions(JsonHelper.Default) { WriteIndented = Indent >= 0 };
        return TextHelper.EncodeUtf8(JsonSerializer.Serialize(items, options));
    }

    private List<List<Note>> SplitNotes(List<Note> notes, TimeSynchronizer synchronizer)
    {
        if (SplitThreshold < 0 || notes.Count == 0)
            return new List<List<Note>> { notes };
        var result = new List<List<Note>>();
        var current = new List<Note>();
        foreach (var note in notes.OrderBy(note => note.StartPos))
        {
            if (current.Count > 0)
            {
                var previous = current[^1];
                double gapMs = synchronizer.GetDurationSecsFromTicks(previous.EndPos, note.StartPos) * 1000;
                double duration = synchronizer.GetDurationSecsFromTicks(current[0].StartPos, previous.EndPos);
                if (gapMs >= MinInterval && (SplitThreshold == 0 || duration >= SplitThreshold))
                {
                    result.Add(current);
                    current = new List<Note>();
                }
            }
            current.Add(note);
        }
        if (current.Count > 0)
            result.Add(current);
        return result;
    }

    private DsItem GenerateItem(
        List<Note> notes,
        SingingTrack track,
        TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatures)
    {
        double offset = notes.Count > 0 ? synchronizer.GetActualSecsFromTicks(notes[0].StartPos) : 0;
        var item = new DsItem
        {
            Offset = offset,
            Seed = Seed >= 0 ? Seed : null,
            InputType = "phoneme",
            NoteDur = new List<double>(),
            NoteSlur = new List<int>(),
            IsSlurSeq = new List<int>(),
            PhDur = new List<double>(),
            PhNum = new List<int>(),
            NoteDurSeq = new List<double>(),
        };
        double cursor = offset;
        foreach (var note in notes)
        {
            double start = synchronizer.GetActualSecsFromTicks(note.StartPos);
            double end = synchronizer.GetActualSecsFromTicks(note.EndPos);
            if (start > cursor + 0.000001)
                AddDsToken(item, "SP", "SP", "rest", start - cursor, 0);
            string lyric = note.Lyric == "-" ? "-" : Regex.Replace(note.Lyric, @"[\p{P}\p{S}]", "");
            string phoneme = note.Pronunciation ?? (lyric == "-" ? item.PhSeq.LastOrDefault() ?? "a" : lyric);
            AddDsToken(item, lyric, phoneme, MusicMath.Midi2Note(note.KeyNumber), end - start, lyric == "-" ? 1 : 0);
            cursor = end;
        }
        AddDsToken(item, "SP", "SP", "rest", 0.05, 0);
        double totalDuration = item.NoteDur!.Sum();
        const double step = 0.005;
        item.F0Timestep = step;
        var signatures = timeSignatures.Count > 0
            ? timeSignatures
            : new List<TimeSignature> { new() };
        var pitchSimulator = new PitchSimulator(
            synchronizer, PortamentoPitch.NoPortamento(), track.NoteList, signatures);
        int firstBar = (int)Math.Round(signatures[0].BarLength());
        pitchSimulator.MergePitchCurve(track.EditedParams.Pitch, firstBar);
        item.F0Seq = new List<double>();
        for (double secs = offset; secs < offset + totalDuration; secs += step)
        {
            double? pitch = pitchSimulator.PitchAtSecs(secs);
            item.F0Seq.Add(pitch.HasValue ? MusicMath.Midi2Hz(pitch.Value / 100) : 0);
        }
        if (ExportGender)
        {
            item.GenderTimestep = step;
            item.Gender = new List<double>();
            for (double secs = offset; secs < offset + totalDuration; secs += step)
            {
                int tick = (int)Math.Round(synchronizer.GetActualTicksFromSecs(secs)) + firstBar;
                item.Gender.Add(SampleCurve(track.EditedParams.Gender, tick) / 1000.0);
            }
        }
        return item;
    }

    private static void AddDsToken(
        DsItem item,
        string lyric,
        string phoneme,
        string noteName,
        double duration,
        int slur)
    {
        item.Text.Add(string.IsNullOrEmpty(lyric) ? "la" : lyric);
        item.PhSeq.Add(string.IsNullOrEmpty(phoneme) ? "a" : phoneme);
        item.NoteSeq.Add(noteName);
        item.NoteDur!.Add(Math.Round(Math.Max(duration, 0.001), 6));
        item.NoteDurSeq!.Add(Math.Round(Math.Max(duration, 0.001), 6));
        item.NoteSlur!.Add(slur);
        item.IsSlurSeq!.Add(slur);
        item.PhDur!.Add(Math.Round(Math.Max(duration, 0.001), 6));
        item.PhNum!.Add(1);
    }

    private static int SampleCurve(ParamCurve curve, int tick)
    {
        var points = curve.Points.Where(point => point.X != Point.StartX && point.X != Point.EndX)
            .OrderBy(point => point.X).ToList();
        if (points.Count == 0)
            return 0;
        int index = Search.FindLastIndex(points, point => point.X <= tick);
        if (index < 0)
            return points[0].Y;
        if (index >= points.Count - 1)
            return points[^1].Y;
        var left = points[index];
        var right = points[index + 1];
        return (int)Math.Round(MusicMath.LinearInterpolation(tick, (left.X, left.Y), (right.X, right.Y)));
    }
}
