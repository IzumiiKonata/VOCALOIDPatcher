using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;

public sealed class UfdataParser
{
    private readonly UfdataInputOptions _options;
    private int _firstBarLength;
    private List<TimeSignature> _timeSignatures = new();

    public UfdataParser(UfdataInputOptions options) => _options = options;

    public Project ParseProject(UFData ufdataProject)
    {
        var ufProject = ufdataProject.Project;
        _timeSignatures = ParseTimeSignatures(ufProject.TimeSignatures);
        if (_timeSignatures.Count == 0)
            _timeSignatures.Add(new TimeSignature());
        _firstBarLength = (int)System.Math.Round(_timeSignatures[0].BarLength());
        int tickPrefix = (int)(_timeSignatures[0].BarLength() * ufProject.MeasurePrefix);
        var songTempoList = TickCounter.ShiftTempoList(ParseTempos(ufProject.Tempos), tickPrefix);
        return new Project
        {
            SongTempoList = songTempoList,
            TimeSignatureList = TickCounter.ShiftBeatList(_timeSignatures, ufProject.MeasurePrefix),
            TrackList = ParseTracks(ufProject.Tracks, tickPrefix).Cast<Track>().ToList(),
        };
    }

    private static List<SongTempo> ParseTempos(List<UFTempos> tempos) =>
        tempos.Select(t => new SongTempo(t.TickPosition, t.Bpm)).ToList();

    private static List<TimeSignature> ParseTimeSignatures(List<UFTimeSignatures> timeSignatures) =>
        timeSignatures
            .Select(t => new TimeSignature(t.MeasurePosition, t.Numerator, t.Denominator))
            .ToList();

    private List<SingingTrack> ParseTracks(List<UFTracks> tracks, int tickPrefix)
    {
        var trackList = new List<SingingTrack>();
        foreach (var track in tracks)
        {
            var singingTrack = new SingingTrack
            {
                Title = track.Name,
                NoteList = ParseNotes(track.Notes, tickPrefix),
            };
            if (_options.ImportPitch && track.Pitch != null)
                singingTrack.EditedParams.Pitch = ParsePitch(track.Pitch, singingTrack.NoteList, tickPrefix);
            trackList.Add(singingTrack);
        }
        return trackList;
    }

    private ParamCurve ParsePitch(UFPitch pitch, List<Note> noteList, int tickPrefix)
    {
        if (!pitch.IsAbsolute)
            return new ParamCurve();

        var pitchPoints = new List<Point> { Point.StartPoint() };
        var prevPoint = pitchPoints[^1];
        Point? point = null;
        int count = System.Math.Min(pitch.Ticks.Count, pitch.Values.Count);
        for (int idx = 0; idx < count; idx++)
        {
            int tick = pitch.Ticks[idx];
            double? value = pitch.Values[idx];
            if (value == 0 && prevPoint.Y == -100)
                pitchPoints.Add(new Point(tick + tickPrefix + _firstBarLength, -100));
            if (value != null)
                point = new Point(tick + tickPrefix + _firstBarLength, (int)System.Math.Round(value.Value * 100));
            if (point != null)
                pitchPoints.Add(point.Value);
            if (value == 0 && prevPoint.Y != -100)
                pitchPoints.Add(new Point(tick + tickPrefix + _firstBarLength, -100));
            if (point != null)
                prevPoint = point.Value;
        }
        if (prevPoint.Y == 0)
            pitchPoints.Add(prevPoint.WithY(-100));
        pitchPoints.Add(Point.EndPoint());
        return new ParamCurve { Points = pitchPoints };
    }

    private static List<Note> ParseNotes(List<UFNotes> notes, int tickPrefix) =>
        notes.Select(note => new Note
        {
            StartPos = note.TickOn + tickPrefix,
            Length = note.TickOff - note.TickOn,
            KeyNumber = note.Key,
            Lyric = note.Lyric,
        }).ToList();
}
