using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Y77;

public sealed class Y77Converter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public double Tempo { get; set; } = Constants.DefaultBpm;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var y77 = JsonHelper.Deserialize<Y77Project>(TextHelper.DetectAndDecode(content));
        int firstBarLength = (int)Math.Round(new TimeSignature(0, y77.BBar, y77.BBeat).BarLength());
        var track = new SingingTrack { AiSingerName = "元七七", NoteList = ParseNotes(y77.Notes) };
        if (ImportPitch)
            track.EditedParams.Pitch = ParsePitch(y77.Notes, firstBarLength);
        return new Project
        {
            SongTempoList = new List<SongTempo> { new(0, y77.Bpm) },
            TimeSignatureList = new List<TimeSignature> { new(0, y77.BBar, y77.BBeat) },
            TrackList = new List<Track> { track },
        };
    }

    private static List<Note> ParseNotes(List<Y77Note> notes) =>
        notes.Select(n => new Note
        {
            Lyric = n.Lyric,
            StartPos = n.Start * 30,
            Length = n.Length * 30,
            KeyNumber = 88 - n.Pitch,
            Pronunciation = string.IsNullOrEmpty(n.Py) ? null : n.Py,
        }).ToList();

    private static ParamCurve ParsePitch(List<Y77Note> notes, int firstBarLength)
    {
        var curve = new ParamCurve();
        foreach (var note in notes)
        {
            if (note.Pit.Count == 0)
                continue;
            double step = note.Length * 30.0 / (note.Pit.Count - 1);
            int pbs = note.Pbs + 1;
            curve.Points.Add(new Point(note.Start * 30 - 5 + firstBarLength, -100));
            for (int i = 0; i < note.Pit.Count; i++)
                curve.Points.Add(new Point(
                    (int)Math.Round(note.Start * 30 + i * step) + firstBarLength,
                    (int)Math.Round(((note.Pit[i] - 50) / 50 * pbs + 88 - note.Pitch) * 100)));
            curve.Points.Add(new Point((note.Start + note.Length) * 30 + 5 + firstBarLength, -100));
        }
        return curve;
    }

    public override byte[] Dump(Project project)
    {
        var source = project.SongTempoList.Count != 1 ? ResetTimeAxis.Reset(project, Tempo) : project;
        var track = source.TrackList.OfType<SingingTrack>().FirstOrDefault(t => t.NoteList.Count > 0)
                    ?? source.TrackList.OfType<SingingTrack>().FirstOrDefault();
        var timeSignature = source.TimeSignatureList.Count > 0 ? source.TimeSignatureList[0] : new TimeSignature();
        int firstBarLength = (int)new TimeSignature(0, timeSignature.Numerator, timeSignature.Denominator).BarLength();

        var y77 = new Y77Project
        {
            Bars = 100,
            Bpm = source.SongTempoList.Count > 0 ? source.SongTempoList[0].Bpm : Constants.DefaultBpm,
            BBar = timeSignature.Numerator,
            BBeat = timeSignature.Denominator,
        };
        if (track != null)
        {
            y77.Notes = GenerateNotes(track, source.SongTempoList, timeSignature, firstBarLength);
            y77.NNote = y77.Notes.Count;
        }
        return TextHelper.EncodeUtf8(JsonHelper.Serialize(y77));
    }

    private static List<Y77Note> GenerateNotes(SingingTrack track, List<SongTempo> tempos, TimeSignature timeSignature, int firstBarLength)
    {
        var synchronizer = new TimeSynchronizer(tempos, firstBarLength);
        var pitchSimulator = new PitchSimulator(synchronizer, PortamentoPitch.NoPortamento(), track.NoteList, new List<TimeSignature> { timeSignature });
        pitchSimulator.MergePitchCurve(track.EditedParams.Pitch, firstBarLength);

        var result = new List<Y77Note>();
        foreach (var note in track.NoteList)
        {
            var (pit, pbs) = GeneratePitch(pitchSimulator, note);
            result.Add(new Y77Note
            {
                Lyric = note.Lyric,
                Start = (int)Math.Round(note.StartPos / 30.0),
                Length = (int)Math.Round(note.Length / 30.0),
                Pitch = 88 - note.KeyNumber,
                Py = note.Pronunciation ?? note.Lyric,
                Pit = pit,
                Pbs = pbs,
            });
        }
        return result;
    }

    private static (List<double>, int) GeneratePitch(PitchSimulator pitchSimulator, Note note)
    {
        double tickStep = note.Length / 500.0;
        var relPitchValues = new List<double>(500);
        for (int i = 0; i < 500; i++)
        {
            double? pitchValue = pitchSimulator.PitchAtTicks(note.StartPos + (int)(tickStep * i));
            double value = pitchValue != null ? pitchValue.Value / 100 : note.KeyNumber;
            relPitchValues.Add(value - note.KeyNumber);
        }
        double maxAbs = relPitchValues.Max(Math.Abs);
        int pbs = Math.Min((int)Math.Ceiling(maxAbs), 12);
        var pit = relPitchValues
            .Select(v => (pbs > 0 ? v * 50 / pbs : 0) + 50)
            .ToList();
        return (pit, pbs - 1);
    }
}
