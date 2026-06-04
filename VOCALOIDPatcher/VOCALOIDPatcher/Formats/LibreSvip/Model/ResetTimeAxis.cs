using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public static class ResetTimeAxis
{
    public static Project Reset(Project project, double tempo = Constants.DefaultBpm)
    {
        var synchronizer = new TimeSynchronizer(project.SongTempoList, isAbsoluteTimeCode: true, defaultTempo: tempo);
        var newTimeSignature = new TimeSignature(0, 4, 4);
        int newFirstBarTicks = (int)Math.Round(newTimeSignature.BarLength());
        int oriFirstBarTicks = (int)Math.Round(project.TimeSignatureList[0].BarLength());

        ParamCurve UpdateCurve(ParamCurve curve) => new()
        {
            Points = curve.Points
                .Select(p => p.WithX((int)Math.Round(synchronizer.GetActualTicksFromTicks(p.X - oriFirstBarTicks)) + newFirstBarTicks))
                .ToList(),
        };

        var newTrackList = new List<Track>();
        foreach (var track in project.TrackList)
        {
            if (track is InstrumentalTrack instrumental)
            {
                newTrackList.Add(new InstrumentalTrack
                {
                    Title = instrumental.Title,
                    Mute = instrumental.Mute,
                    Solo = instrumental.Solo,
                    Volume = instrumental.Volume,
                    Pan = instrumental.Pan,
                    AudioFilePath = instrumental.AudioFilePath,
                    Offset = (int)Math.Round(synchronizer.GetActualTicksFromTicks(instrumental.Offset)),
                });
            }
            else if (track is SingingTrack singing)
            {
                var newNotes = new List<Note>();
                foreach (var note in singing.NoteList)
                {
                    int noteEnd = (int)Math.Round(synchronizer.GetActualTicksFromTicks(note.EndPos));
                    int noteStart = (int)Math.Round(synchronizer.GetActualTicksFromTicks(note.StartPos));
                    newNotes.Add(new Note
                    {
                        StartPos = noteStart,
                        Length = noteEnd - noteStart,
                        KeyNumber = note.KeyNumber,
                        HeadTag = note.HeadTag,
                        Lyric = note.Lyric,
                        Pronunciation = note.Pronunciation,
                        EditedPhones = note.EditedPhones,
                        Vibrato = note.Vibrato,
                    });
                }
                newTrackList.Add(new SingingTrack
                {
                    Title = singing.Title,
                    Mute = singing.Mute,
                    Solo = singing.Solo,
                    Volume = singing.Volume,
                    Pan = singing.Pan,
                    AiSingerName = singing.AiSingerName,
                    ReverbPreset = singing.ReverbPreset,
                    NoteList = newNotes,
                    EditedParams = new Params
                    {
                        Pitch = UpdateCurve(singing.EditedParams.Pitch),
                        Volume = UpdateCurve(singing.EditedParams.Volume),
                        Breath = UpdateCurve(singing.EditedParams.Breath),
                        Gender = UpdateCurve(singing.EditedParams.Gender),
                        Strength = UpdateCurve(singing.EditedParams.Strength),
                    },
                });
            }
        }

        return new Project
        {
            SongTempoList = new List<SongTempo> { new(0, tempo) },
            TimeSignatureList = new List<TimeSignature> { newTimeSignature },
            TrackList = newTrackList,
        };
    }
}
