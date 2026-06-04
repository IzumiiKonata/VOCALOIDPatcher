using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ufdata;

public sealed class UfdataGenerator
{
    private readonly UfdataOutputOptions _options;
    private int _firstBarLength;

    public UfdataGenerator(UfdataOutputOptions options) => _options = options;

    public UFData GenerateProject(Project project)
    {
        _firstBarLength = (int)System.Math.Round(project.TimeSignatureList[0].BarLength());
        return new UFData
        {
            Project = new UFProject
            {
                Tempos = GenerateTempos(project.SongTempoList),
                TimeSignatures = GenerateTimeSignatures(project.TimeSignatureList),
                Tracks = GenerateTracks(project.TrackList),
                MeasurePrefix = 0,
            },
        };
    }

    private List<UFTempos> GenerateTempos(List<SongTempo> songTempoList) =>
        TickCounter.SkipTempoList(songTempoList, _firstBarLength)
            .Select(t => new UFTempos { TickPosition = t.Position, Bpm = t.Bpm })
            .ToList();

    private static List<UFTimeSignatures> GenerateTimeSignatures(List<TimeSignature> timeSignatureList) =>
        timeSignatureList
            .Select(t => new UFTimeSignatures
            {
                MeasurePosition = t.BarIndex,
                Numerator = t.Numerator,
                Denominator = t.Denominator,
            })
            .ToList();

    private List<UFTracks> GenerateTracks(List<Track> trackList) =>
        trackList.OfType<SingingTrack>()
            .Select(track => new UFTracks
            {
                Name = track.Title,
                Notes = GenerateNotes(track.NoteList),
                Pitch = GeneratePitch(track.EditedParams.Pitch, track.NoteList),
            })
            .ToList();

    private static List<UFNotes> GenerateNotes(List<Note> noteList) =>
        noteList.Select(note => new UFNotes
        {
            TickOn = note.StartPos,
            TickOff = note.EndPos,
            Lyric = note.Lyric,
            Key = note.KeyNumber,
        }).ToList();

    private UFPitch GeneratePitch(ParamCurve pitch, List<Note> notes)
    {
        var ufPitch = new UFPitch { IsAbsolute = true };
        if (notes.Count > 0)
        {
            foreach (var point in pitch.Points)
            {
                if (point.X != Point.StartX && point.X != Point.EndX)
                {
                    ufPitch.Ticks.Add(point.X - _firstBarLength);
                    ufPitch.Values.Add(point.Y == -100 ? 0 : point.Y / 100.0);
                }
            }
        }
        return ufPitch;
    }
}
