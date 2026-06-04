using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;

public sealed class S5pParser
{
    private const long TickRate = 1470000;

    private readonly bool _importPitch;
    private readonly bool _importInstrumental;
    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public S5pParser(bool importPitch, bool importInstrumental)
    {
        _importPitch = importPitch;
        _importInstrumental = importInstrumental;
    }

    public Project ParseProject(S5pProject project)
    {
        var timeSignatures = ParseTimeSignatures(project.Meter);
        _firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());
        var tempoList = ParseTempos(project.Tempo);
        _synchronizer = new TimeSynchronizer(tempoList);
        var trackList = ParseSingingTracks(project.Tracks);
        if (_importInstrumental && !string.IsNullOrEmpty(project.Instrumental.Filename))
            trackList.Add(ParseInstrumentalTrack(project.Instrumental, project.Mixer));
        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempoList,
            TrackList = trackList,
        };
    }

    private static List<TimeSignature> ParseTimeSignatures(List<S5pMeterItem> meter)
    {
        var result = meter
            .Select(item => new TimeSignature(item.Measure, item.BeatPerMeasure, item.BeatGranularity))
            .ToList();
        if (result.Count == 0)
            result.Add(new TimeSignature(0, 4, 4));
        return result;
    }

    private List<SongTempo> ParseTempos(List<S5pTempoItem> tempo) =>
        TickCounter.ShiftTempoList(
            tempo.Select(item => new SongTempo((int)(item.Position / TickRate), item.BeatPerMinute)).ToList(),
            _firstBarLength);

    private List<Track> ParseSingingTracks(List<S5pTrack> tracks)
    {
        var result = new List<Track>();
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var noteList = ParseNotes(track.Notes, track.DbDefaults);
            if (noteList.Count == 0)
                continue;
            var singing = new SingingTrack
            {
                Mute = track.Mixer.Muted,
                Solo = track.Mixer.Solo,
                Volume = ParseVolume(track.Mixer.GainDecibel),
                Pan = track.Mixer.Pan,
                AiSingerName = track.DbName,
                Title = string.IsNullOrEmpty(track.Name) ? $"Track {i + 1}" : track.Name,
                NoteList = noteList,
            };
            if (_importPitch)
            {
                var nonNull = track.Notes.Where(n => n != null).Select(n => n!).ToList();
                singing.EditedParams.Pitch = ParsePitch(track.Parameters, nonNull, track.DbDefaults);
            }
            result.Add(singing);
        }
        return result;
    }

    private static double ParseVolume(double gain) =>
        gain >= 0 ? Math.Min(gain / MusicMath.RatioToDb(4) + 1.0, 2.0) : MusicMath.DbToFloat(gain);

    private InstrumentalTrack ParseInstrumentalTrack(S5pInstrumental track, S5pMixer mixer) => new()
    {
        Mute = mixer.InstrumentalMuted,
        Volume = ParseVolume(mixer.GainInstrumentalDecibel),
        Title = System.IO.Path.GetFileName(track.Filename),
        AudioFilePath = track.Filename,
        Offset = (int)Math.Round(_synchronizer.GetActualTicksFromSecs(track.Offset)),
    };

    private List<Note> ParseNotes(List<S5pNote?> notes, S5pDbDefaults dbDefaults)
    {
        var result = new List<Note>();
        foreach (var s5pNote in notes)
        {
            if (s5pNote == null)
                continue;
            string cleaned = s5pNote.Lyric.TrimStart('.').Replace(" ", "");
            string lyric = string.IsNullOrEmpty(cleaned) ? dbDefaults.Lyric ?? "" : cleaned;
            string? pronunciation = s5pNote.Lyric.StartsWith(".", StringComparison.Ordinal) && !string.IsNullOrEmpty(s5pNote.Comment)
                ? s5pNote.Comment
                : null;
            result.Add(new Note
            {
                KeyNumber = s5pNote.Pitch,
                StartPos = (int)Math.Round(s5pNote.Onset / (double)TickRate),
                Length = (int)Math.Round(s5pNote.Duration / (double)TickRate),
                Lyric = lyric,
                Pronunciation = pronunciation,
            });
        }
        return result;
    }

    private ParamCurve ParsePitch(S5pParameters parameters, List<S5pNote> noteList, S5pDbDefaults dbDefaults)
    {
        var noteStructs = noteList.Select(n => NoteToNoteStruct(n, dbDefaults)).ToList();
        var pitchSimulator = new SynthVPitchSimulator(_synchronizer, noteStructs);

        double tickScale = parameters.Interval / (double)TickRate;
        var relativeGroups = IterTools
            .SplitWhen(parameters.PitchDelta, (p1, p2) => p2.Offset > p1.Offset + 1)
            .Where(g => g.Count > 1)
            .Select(g => g.Select(p => new Point((int)Math.Round(p.Offset * tickScale), (int)Math.Round(p.Value))).ToList())
            .ToList();

        var points = new List<Point> { Point.StartPoint() };
        foreach (var (startSecs, endSecs) in IterNoteSegments(noteStructs))
        {
            int startTicks = (int)Math.Round(_synchronizer.GetActualTicksFromSecs(startSecs));
            int endTicks = (int)Math.Round(_synchronizer.GetActualTicksFromSecs(endSecs));
            points.Add(new Point(startTicks + _firstBarLength, -100));
            for (int ticks = startTicks; ticks < endTicks; ticks += 5)
            {
                double value = pitchSimulator.PitchAtTicks(ticks) + RelativePitchAtTicks(ticks, relativeGroups);
                points.Add(new Point(ticks + _firstBarLength, (int)Math.Round(value)));
            }
            double endValue = pitchSimulator.PitchAtTicks(endTicks) + RelativePitchAtTicks(endTicks, relativeGroups);
            points.Add(new Point(endTicks + _firstBarLength, (int)Math.Round(endValue)));
            points.Add(new Point(endTicks + _firstBarLength, -100));
        }
        points.Add(Point.EndPoint());
        return new ParamCurve { Points = points };
    }

    private static List<(double Start, double End)> IterNoteSegments(List<NoteStruct> noteStructs)
    {
        var segments = new List<(double, double)>();
        if (noteStructs.Count == 0)
            return segments;
        double startSecs = noteStructs[0].Start;
        double endSecs = noteStructs[0].End;
        for (int i = 1; i < noteStructs.Count; i++)
        {
            var note = noteStructs[i];
            if (note.Start - endSecs > 0.01)
            {
                segments.Add((startSecs, endSecs));
                startSecs = note.Start;
            }
            endSecs = note.End;
        }
        segments.Add((startSecs, endSecs));
        return segments;
    }

    private static double RelativePitchAtTicks(int ticks, List<List<Point>> relativeGroups)
    {
        foreach (var points in relativeGroups)
        {
            if (points.Count == 0)
                continue;
            if (ticks < points[0].X || ticks > points[^1].X)
                continue;
            if (points.Count == 1)
                return points[0].Y;
            if (ticks <= points[0].X)
                return points[0].Y;
            if (ticks >= points[^1].X)
                return points[^1].Y;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var prev = points[i];
                var point = points[i + 1];
                if (prev.X <= ticks && ticks <= point.X)
                    return MusicMath.LinearInterpolation(ticks, (prev.X, prev.Y), (point.X, point.Y));
            }
        }
        return 0.0;
    }

    private NoteStruct NoteToNoteStruct(S5pNote note, S5pDbDefaults db)
    {
        int onset = (int)Math.Round(note.Onset / (double)TickRate);
        int endPos = onset + (int)Math.Round(note.Duration / (double)TickRate);
        return new NoteStruct(
            note.Pitch,
            _synchronizer.GetActualSecsFromTicks(onset),
            _synchronizer.GetActualSecsFromTicks(endPos),
            note.TF0Offset ?? 0.0,
            note.TF0Left ?? db.TF0Left,
            note.TF0Right ?? db.TF0Right,
            note.DF0Left ?? db.DF0Left,
            note.DF0Right ?? db.DF0Right,
            note.TF0VbrStart ?? db.TF0VbrStart,
            note.TF0VbrLeft ?? db.TF0VbrLeft,
            note.TF0VbrRight ?? db.TF0VbrRight,
            (note.DF0Vbr ?? db.DF0Vbr) * 40,
            note.FF0Vbr ?? db.FF0Vbr,
            Math.PI * (note.PF0Vbr ?? db.PF0Vbr));
    }
}
