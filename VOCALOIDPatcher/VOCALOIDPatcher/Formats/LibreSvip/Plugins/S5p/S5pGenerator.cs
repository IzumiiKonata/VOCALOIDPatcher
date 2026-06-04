using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.S5p;

public sealed class S5pGenerator
{
    private const long TickRate = 1470000;

    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public S5pProject GenerateProject(Project project)
    {
        _firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        var s5pProject = new S5pProject
        {
            Tempo = GenerateTempos(project.SongTempoList),
            Meter = GenerateTimeSignatures(project.TimeSignatureList),
            Tracks = GenerateSingingTracks(project.TrackList),
        };
        var instrumental = project.TrackList.OfType<InstrumentalTrack>().FirstOrDefault();
        if (instrumental != null)
        {
            s5pProject.Instrumental = new S5pInstrumental
            {
                Filename = instrumental.AudioFilePath,
                Offset = _synchronizer.GetActualSecsFromTicks(instrumental.Offset),
            };
            s5pProject.Mixer = new S5pMixer
            {
                GainInstrumentalDecibel = GenerateVolume(instrumental.Volume),
                InstrumentalMuted = instrumental.Mute,
            };
        }
        return s5pProject;
    }

    private List<S5pTempoItem> GenerateTempos(List<SongTempo> songTempoList)
    {
        var shifted = TickCounter.SkipTempoList(songTempoList, _firstBarLength);
        _synchronizer = new TimeSynchronizer(shifted);
        return shifted.Select(t => new S5pTempoItem { Position = (long)t.Position * TickRate, BeatPerMinute = t.Bpm }).ToList();
    }

    private static List<S5pMeterItem> GenerateTimeSignatures(List<TimeSignature> timeSignatureList) =>
        timeSignatureList.Select(t => new S5pMeterItem
        {
            Measure = t.BarIndex,
            BeatPerMeasure = t.Numerator,
            BeatGranularity = t.Denominator,
        }).ToList();

    private static double GenerateVolume(double volume) =>
        volume > 0 ? Math.Max(MusicMath.RatioToDb(Math.Max(volume, 0.01)), -70) : -70;

    private List<S5pTrack> GenerateSingingTracks(List<Track> trackList)
    {
        var tracks = new List<S5pTrack>();
        for (int i = 0; i < trackList.Count; i++)
        {
            if (trackList[i] is not SingingTrack track)
                continue;
            var s5pNotes = GenerateNotes(track.NoteList);
            var s5pTrack = new S5pTrack
            {
                DisplayOrder = i,
                Name = track.Title,
                DbName = track.AiSingerName,
                Notes = s5pNotes.Cast<S5pNote?>().ToList(),
                Mixer = new S5pTrackMixer
                {
                    Solo = track.Solo,
                    Muted = track.Mute,
                    Pan = track.Pan,
                    GainDecibel = GenerateVolume(track.Volume),
                },
            };
            s5pTrack.Parameters = GenerateParameters(track.EditedParams, s5pNotes, s5pTrack.DbDefaults);
            tracks.Add(s5pTrack);
        }
        if (tracks.Count == 0)
            tracks.Add(new S5pTrack { Name = "Unnamed Track", Notes = new List<S5pNote?> { null } });
        return tracks;
    }

    private static List<S5pNote> GenerateNotes(List<Note> noteList) =>
        noteList.Select(note => new S5pNote
        {
            Lyric = note.Lyric,
            Onset = (long)note.StartPos * TickRate,
            Duration = (long)note.Length * TickRate,
            Pitch = note.KeyNumber,
        }).ToList();

    private S5pParameters GenerateParameters(Params editedParams, List<S5pNote> noteList, S5pDbDefaults dbDefaults)
    {
        long interval = (long)Math.Round(TickRate * 3.75);
        var noteStructs = noteList.Select(n => NoteToNoteStruct(n, dbDefaults)).ToList();
        var pitchSimulator = new SynthVPitchSimulator(_synchronizer, noteStructs);
        return new S5pParameters
        {
            PitchDelta = GeneratePitchDelta(editedParams.Pitch.Points, pitchSimulator, interval),
            Interval = interval,
        };
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

    private List<S5pPoint> GeneratePitchDelta(List<Point> pitch, SynthVPitchSimulator pitchSimulator, long interval)
    {
        double tickScale = interval / (double)TickRate;
        long upperLimit = long.MaxValue / 2;
        var relativePoints = new List<S5pPoint>();
        foreach (var point in pitch)
        {
            if (point.Y == -100 || point.X < _firstBarLength || point.X >= upperLimit)
                continue;
            relativePoints.Add(new S5pPoint(
                Math.Round((point.X - _firstBarLength) / tickScale),
                point.Y - pitchSimulator.PitchAtTicks(point.X - _firstBarLength)));
        }
        return CompressPitchDelta(relativePoints);
    }

    private static List<S5pPoint> CompressPitchDelta(List<S5pPoint> relativePoints)
    {
        if (relativePoints.Count == 0)
            return new List<S5pPoint>();
        var compressed = new List<S5pPoint>();
        const double tolerance = 1e-3;
        foreach (var group in IterTools.SplitWhen(relativePoints, (p1, p2) => p2.Offset > p1.Offset + 1))
        {
            if (group.Count == 0)
                continue;
            var nonzero = new List<int>();
            for (int i = 0; i < group.Count; i++)
                if (Math.Abs(group[i].Value) > tolerance)
                    nonzero.Add(i);
            if (nonzero.Count == 0)
                continue;
            if (group.Count == 1)
            {
                compressed.Add(group[0]);
                compressed.Add(group[0]);
                continue;
            }
            int startIndex = Math.Max(0, nonzero[0] - 1);
            int endIndex = Math.Min(group.Count - 1, nonzero[^1] + 1);
            for (int i = startIndex; i <= endIndex; i++)
                compressed.Add(group[i]);
        }
        return compressed;
    }
}
