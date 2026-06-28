using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public sealed class SvpConverter : FormatConverter
{
    private const long TickRate = 1470000;

    private static readonly HashSet<char> SymbolBlacklist = new(
        "()[]{}（）<>《》―—*×!！?？:：·•。,，;；^`\"‘’“”=、_$%~@#…&￥");

    public bool ImportInstrumental { get; set; } = true;
    public bool ImportPitch { get; set; } = true;

    private int _firstBarTick;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    private static int PositionToTicks(long position) => (int)Math.Round(position / (double)TickRate);
    private static long TicksToPosition(int ticks) => (long)ticks * TickRate;

    public override Project Load(byte[] content)
    {
        string text = TextHelper.DetectAndDecode(content).Trim('\0').Trim();
        var svp = JsonHelper.Deserialize<SVProject>(text);

        var timeSignatures = TickCounter.ShiftBeatList(
            svp.Time.Meter.Select(m => new TimeSignature(m.Index, m.Numerator, m.Denominator)).ToList(), 1);
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        _firstBarTick = (int)Math.Round(timeSignatures[0].BarLength());
        var tempos = TickCounter.ShiftTempoList(
            svp.Time.Tempo.Select(t => new SongTempo(PositionToTicks(t.Position), t.Bpm)).ToList(), _firstBarTick);
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());
        var synchronizer = new TimeSynchronizer(tempos);

        var library = new Dictionary<string, SVGroup>();
        foreach (var group in svp.Library)
            library[group.Uuid] = group;
        var splitCounts = new Dictionary<string, int>();

        var trackList = new List<Track>();
        var groupTracks = new List<Track>();
        foreach (var svTrack in svp.Tracks)
        {
            if (svTrack.MainRef.IsInstrumental)
            {
                if (ImportInstrumental && svTrack.MainRef.Audio != null)
                    trackList.Add(new InstrumentalTrack
                    {
                        Title = svTrack.Name,
                        AudioFilePath = svTrack.MainRef.Audio.Filename,
                        Offset = PositionToTicks(svTrack.MainRef.BlickOffset),
                        Mute = svTrack.Mixer.Mute,
                        Solo = svTrack.Mixer.Solo,
                        Pan = svTrack.Mixer.Pan,
                        Volume = ParseVolume(svTrack.Mixer.GainDecibel),
                    });
                continue;
            }

            var mainSinging = new SingingTrack
            {
                Title = svTrack.Name,
                AiSingerName = svTrack.MainRef.Database.Name,
                Mute = svTrack.Mixer.Mute,
                Solo = svTrack.Mixer.Solo,
                Pan = svTrack.Mixer.Pan,
                Volume = ParseVolume(svTrack.Mixer.GainDecibel),
                NoteList = ParseNotes(svTrack.MainGroup.Notes, 0, 0),
            };
            if (ImportPitch && svTrack.MainGroup.Notes.Count > 0)
            {
                var pitch = ParsePitch(svTrack.MainGroup.Parameters, svTrack.MainGroup.Notes, timeSignatures, synchronizer);
                if (pitch != null)
                    mainSinging.EditedParams.Pitch = pitch;
            }
            trackList.Add(mainSinging);

            foreach (var svRef in svTrack.Groups)
            {
                if (!library.TryGetValue(svRef.GroupId, out var group))
                    continue;
                splitCounts.TryGetValue(svRef.GroupId, out int count);
                splitCounts[svRef.GroupId] = count + 1;
                groupTracks.Add(new SingingTrack
                {
                    Title = $"{group.Name} ({count + 1})",
                    AiSingerName = svRef.Database.Name,
                    NoteList = ParseNotes(group.Notes, svRef.BlickOffset, svRef.PitchOffset),
                });
            }
        }
        trackList.AddRange(groupTracks);

        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = trackList,
        };
    }

    private static List<Note> ParseNotes(List<SVNote> notes, long blickOffset, int pitchOffset)
    {
        var result = new List<Note>();
        foreach (var svNote in notes)
        {
            long onset = svNote.Onset + blickOffset;
            if (onset < 0)
                continue;
            int start = PositionToTicks(onset);
            result.Add(new Note
            {
                StartPos = start,
                Length = PositionToTicks(onset + svNote.Duration) - start,
                KeyNumber = svNote.Pitch + pitchOffset,
                Lyric = NormalizeLyric(svNote.Lyrics),
                Pronunciation = string.IsNullOrEmpty(svNote.Phonemes) ? null : svNote.Phonemes,
            });
        }
        return result;
    }

    private ParamCurve? ParsePitch(SVParameters parameters, List<SVNote> svNotes,
        List<TimeSignature> timeSignatures, TimeSynchronizer synchronizer)
    {
        if (svNotes.Count == 0)
            return null;
        var pitchDiff = new CurveGenerator(
            parameters.PitchDelta.Points.Select(p => new Point(PositionToTicks(p.Offset), (int)Math.Round(p.Value))),
            Interp(parameters.PitchDelta.Mode), 0);
        var vibratoEnv = new CurveGenerator(
            parameters.VibratoEnv.Points.Select(p => new Point(PositionToTicks(p.Offset), (int)Math.Round(p.Value * 1000))),
            Interp(parameters.VibratoEnv.Mode), 1000);
        var noteStructs = svNotes.Select(n => ToNoteStruct(n, synchronizer)).ToList();
        var generator = new PitchGenerator(synchronizer, noteStructs, pitchDiff, vibratoEnv, null);
        var interval = new RangeInterval(
            svNotes.Select(n => (PositionToTicks(n.Onset), PositionToTicks(n.Onset + n.Duration)))).Expand(120);

        var points = new List<Point> { Point.StartPoint() };
        foreach (var (start, end) in interval.Shift(_firstBarTick).SubRanges())
        {
            points.Add(new Point(start, -100));
            for (int i = start; i < end; i += 5)
                points.Add(new Point(i, (int)Math.Round(generator.ValueAtTicks(i - _firstBarTick))));
            points.Add(new Point(end, (int)Math.Round(generator.ValueAtTicks(end - _firstBarTick))));
            points.Add(new Point(end, -100));
        }
        points.Add(Point.EndPoint());
        return new ParamCurve { Points = points };
    }

    private static NoteStruct ToNoteStruct(SVNote note, TimeSynchronizer synchronizer)
    {
        var a = note.Attributes;
        return new NoteStruct(
            note.Pitch,
            synchronizer.GetActualSecsFromTicks(PositionToTicks(note.Onset)),
            synchronizer.GetActualSecsFromTicks(PositionToTicks(note.Onset + note.Duration)),
            a.TransitionOffset, a.PortamentoLeft, a.PortamentoRight, a.DepthLeft, a.DepthRight,
            a.VibratoStart, a.VibratoLeft, a.VibratoRight, a.VibratoDepth, a.VibratoFrequency, a.VibratoPhase);
    }

    private static InterpolationFunc Interp(string mode) => mode switch
    {
        "cosine" => MusicMath.CosineEasingInOutInterpolation,
        "cubic" => MusicMath.CubicInterpolation,
        _ => MusicMath.LinearInterpolation,
    };

    private static string NormalizeLyric(string lyric)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in lyric)
            if (!SymbolBlacklist.Contains(c))
                sb.Append(c);
        return sb.ToString().Trim();
    }

    private static double ParseVolume(double gain) =>
        gain >= 0 ? Math.Min(gain / MusicMath.RatioToDb(4) + 1.0, 2.0) : MusicMath.DbToFloat(gain);

    public override byte[] Dump(Project project)
    {
        _firstBarTick = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        var svp = new SVProject();
        svp.Time.Meter = TickCounter.SkipBeatList(project.TimeSignatureList, 1)
            .Select(ts => new SVMeter { Index = ts.BarIndex, Numerator = ts.Numerator, Denominator = ts.Denominator })
            .ToList();
        if (svp.Time.Meter.Count == 0)
            svp.Time.Meter.Add(new SVMeter { Index = 0, Numerator = 4, Denominator = 4 });
        svp.Time.Tempo = TickCounter.SkipTempoList(project.SongTempoList, _firstBarTick)
            .Select(t => new SVTempo { Position = TicksToPosition(t.Position), Bpm = t.Bpm })
            .ToList();
        if (svp.Time.Tempo.Count == 0)
            svp.Time.Tempo.Add(new SVTempo { Position = 0, Bpm = Constants.DefaultBpm });

        var synchronizer = new TimeSynchronizer(project.SongTempoList, _firstBarTick);

        foreach (var track in project.TrackList)
        {
            if (track is SingingTrack singing)
            {
                svp.Tracks.Add(new SVTrack
                {
                    Name = singing.Title,
                    Mixer = new SVMixer { GainDecibel = GenerateVolume(singing.Volume), Pan = singing.Pan, Mute = singing.Mute, Solo = singing.Solo },
                    MainRef = new SVRef { IsInstrumental = false, Database = new SVDatabase { Name = singing.AiSingerName }, GroupId = Guid.NewGuid().ToString() },
                    MainGroup = new SVGroup
                    {
                        Uuid = Guid.NewGuid().ToString(),
                        Notes = singing.NoteList.Select(n => new SVNote
                        {
                            Onset = TicksToPosition(n.StartPos),
                            Duration = TicksToPosition(n.EndPos) - TicksToPosition(n.StartPos),
                            Lyrics = n.Lyric,
                            Phonemes = n.Pronunciation ?? "",
                            Pitch = n.KeyNumber,
                        }).ToList(),
                        Parameters = GenerateParams(singing.EditedParams, singing.NoteList, synchronizer, project.TimeSignatureList),
                    },
                });
            }
            else if (track is InstrumentalTrack instrumental)
            {
                svp.Tracks.Add(new SVTrack
                {
                    Name = instrumental.Title,
                    Mixer = new SVMixer { Mute = instrumental.Mute, Solo = instrumental.Solo },
                    MainRef = new SVRef
                    {
                        IsInstrumental = true,
                        BlickOffset = TicksToPosition(instrumental.Offset),
                        Audio = new SVAudio { Filename = instrumental.AudioFilePath, Duration = 0 },
                        GroupId = Guid.NewGuid().ToString(),
                    },
                });
            }
        }

        string json = JsonHelper.Serialize(svp);
        return TextHelper.EncodeUtf8(json).Concat(new byte[] { 0 }).ToArray();
    }

    private static double GenerateVolume(double volume) =>
        Math.Max(MusicMath.RatioToDb(Math.Max(volume, 0.06)), -24.0);

    private const int DownSample = 20;

    private SVParameters GenerateParams(Params parameters, List<Note> noteList, TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatureList)
    {
        var result = new SVParameters
        {
            Loudness = GenerateParamCurve(parameters.Volume.ReduceSampleRate(DownSample), 0, 0.0, val =>
                val >= 0
                    ? val / 1000.0 * 12.0
                    : Math.Max(MusicMath.RatioToDb(val > -997 ? val / 1000.0 + 1.0 : 0.0039), -48.0)),
            Tension = GenerateParamCurve(parameters.Strength.ReduceSampleRate(DownSample), 0, 0.0, val => 1000.0 / val),
            Breathiness = GenerateParamCurve(parameters.Breath.ReduceSampleRate(DownSample), 0, 0.0, val => 1000.0 / val),
            Gender = GenerateParamCurve(parameters.Gender.ReduceSampleRate(DownSample), 0, 0.0, val => -1000.0 / val),
        };
        result.PitchDelta = GeneratePitchCurve(parameters.Pitch.ReduceSampleRate(DownSample, -100), noteList, synchronizer, timeSignatureList);
        return result;
    }

    private SVParamCurve GeneratePitchCurve(ParamCurve curve, List<Note> noteList, TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatureList)
    {
        var svCurve = new SVParamCurve();
        if (noteList.Count == 0)
            return svCurve;
        var simulator = new PitchSimulator(synchronizer, PortamentoPitch.NoPortamento(), noteList, timeSignatureList);
        var pointList = svCurve.Points;
        var buffer = new List<Point>();
        const int minInterval = 1;
        Point? lastPoint = null;
        foreach (var point in curve.Points)
        {
            if (point.X < _firstBarTick)
                continue;
            var shifted = point.WithX(point.X - _firstBarTick);
            if (shifted.Y == -100)
            {
                if (buffer.Count == 0)
                    continue;
                if (lastPoint is not { } lp || lp.X + minInterval < buffer[0].X)
                {
                    if (lastPoint is { } lp2 && lp2.X + 2 * minInterval < buffer[0].X)
                        pointList.Add(new SvParamPoint(TicksToPosition(lp2.X + minInterval), 0));
                    pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), 0));
                }
                foreach (var tmp in buffer)
                    pointList.Add(new SvParamPoint(
                        TicksToPosition(tmp.X),
                        GeneratePitchDiff(simulator, synchronizer, tmp.X, tmp.Y)));
                lastPoint = buffer[^1];
                buffer.Clear();
            }
            else
                buffer.Add(shifted);
        }
        if (lastPoint is { } last)
            pointList.Add(new SvParamPoint(TicksToPosition(last.X + minInterval), 0));
        return svCurve;
    }

    private static double GeneratePitchDiff(PitchSimulator simulator,
        TimeSynchronizer synchronizer, int pos, int pitch)
    {
        double? simulatedPitch = simulator.PitchAtSecs(synchronizer.GetActualSecsFromTicks(pos));
        if (simulatedPitch != null)
            return pitch - simulatedPitch.Value;
        return 0.0;
    }

    private SVParamCurve GenerateParamCurve(ParamCurve curve, int termination, double defaultValue,
        Func<int, double> mappingFunc)
    {
        var svCurve = new SVParamCurve();
        if (curve.Points.Count == 0)
            return svCurve;
        if (DownSample > 15)
            svCurve.Mode = "cubic";
        int skipped = 0;
        var pointList = svCurve.Points;
        var points = curve.Points;
        if (points[0].X == Point.StartX)
        {
            if (points.Count == 2 && points[1].X == Point.EndX)
            {
                if (points[0].Y != termination)
                    pointList.Add(new SvParamPoint(0, mappingFunc(points[0].Y)));
                return svCurve;
            }
            skipped = 1;
            int validIndex = Search.FindIndex(points, p => p.X >= _firstBarTick);
            if (validIndex != -1 && points.Count > validIndex + 1
                && (points[validIndex].Y != termination
                    || points[validIndex + 1].Y != termination
                    || points[validIndex + 1].X == Point.EndX))
            {
                skipped = validIndex + 1;
                pointList.Add(new SvParamPoint(
                    TicksToPosition(points[validIndex].X - _firstBarTick),
                    mappingFunc(points[validIndex].Y)));
            }
        }
        var buffer = new List<Point>();
        const int minInterval = 1;
        Point? lastPoint = null;
        for (int idx = skipped; idx < points.Count; idx++)
        {
            var point = points[idx];
            if (point.X < _firstBarTick || point.X == Point.EndX)
                continue;
            var shifted = point.WithX(point.X - _firstBarTick);
            if (shifted.Y == termination)
            {
                if (buffer.Count == 0)
                    continue;
                if (lastPoint is not { } lp || lp.X + minInterval < buffer[0].X)
                {
                    if (lastPoint is { } lp2 && lp2.X + 2 * minInterval < buffer[0].X)
                        pointList.Add(new SvParamPoint(TicksToPosition(lp2.X + minInterval), defaultValue));
                    pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), defaultValue));
                }
                foreach (var tmp in buffer)
                    pointList.Add(new SvParamPoint(TicksToPosition(tmp.X), mappingFunc(tmp.Y)));
                lastPoint = buffer[^1];
                buffer.Clear();
            }
            else
                buffer.Add(shifted);
        }
        if (buffer.Count == 0)
        {
            if (lastPoint is { } lpEnd)
                pointList.Add(new SvParamPoint(TicksToPosition(lpEnd.X + minInterval), defaultValue));
            return svCurve;
        }
        if (lastPoint is not { } lpr || lpr.X + minInterval < buffer[0].X)
        {
            if (lastPoint is { } lpr2 && lpr2.X + 2 * minInterval < buffer[0].X)
                pointList.Add(new SvParamPoint(TicksToPosition(lpr2.X + minInterval), defaultValue));
            pointList.Add(new SvParamPoint(TicksToPosition(buffer[0].X - minInterval), defaultValue));
        }
        foreach (var tmp in buffer)
            pointList.Add(new SvParamPoint(TicksToPosition(tmp.X), mappingFunc(tmp.Y)));
        var tail = buffer[^1];
        buffer.Clear();
        if (tail.Y == termination)
            pointList.Add(new SvParamPoint(TicksToPosition(tail.X + minInterval), defaultValue));
        return svCurve;
    }

    private static NoteStruct ToNoteStruct(Note note, TimeSynchronizer synchronizer) =>
        new(
            note.KeyNumber,
            synchronizer.GetActualSecsFromTicks(note.StartPos),
            synchronizer.GetActualSecsFromTicks(note.EndPos),
            0.0, 0.07, 0.07, 0.15, 0.15, 0.25, 0.2, 0.2, 1.0, 5.5, 0.0);
}
