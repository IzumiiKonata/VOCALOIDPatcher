using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Subtitle;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class UstxGenerator
{
    private USTXProject _ustxProject = null!;
    private int _firstBarLength = 1920;

    public USTXProject GenerateProject(Project project)
    {
        var timeSignatures = project.TimeSignatureList
            .Select(ts => new UTimeSignature { BarPosition = ts.BarIndex, BeatPerBar = ts.Numerator, BeatUnit = ts.Denominator })
            .ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new UTimeSignature());
        int firstBarLength = 1920 * timeSignatures[0].BeatPerBar / timeSignatures[0].BeatUnit;
        _firstBarLength = firstBarLength;

        var tempos = project.SongTempoList
            .Select(t => new UTempo { Position = Math.Max(t.Position - firstBarLength, 0), Bpm = t.Bpm })
            .ToList();
        if (tempos.Count == 0)
            tempos.Add(new UTempo());

        var ustxProject = new USTXProject
        {
            UstxVersion = "0.6",
            Tempos = tempos,
            Bpm = tempos[0].Bpm,
            TimeSignatures = timeSignatures,
            Tracks = new List<UTrack>(),
            VoiceParts = new List<UVoicePart>(),
            WaveParts = new List<UWavePart>(),
        };
        _ustxProject = ustxProject;

        for (int trackNo = 0; trackNo < project.TrackList.Count; trackNo++)
        {
            var osTrack = project.TrackList[trackNo];
            var ustxTrack = GenerateTrack(osTrack);
            ustxProject.Tracks.Add(ustxTrack);
            if (osTrack is SingingTrack singing)
            {
                ustxTrack.Singer = singing.AiSingerName;
                ustxProject.VoiceParts.Add(GenerateVoicePart(singing, trackNo));
            }
            else if (osTrack is InstrumentalTrack instrumental)
            {
                ustxProject.WaveParts.Add(new UWavePart
                {
                    Name = instrumental.Title,
                    TrackNo = trackNo,
                    Position = instrumental.Offset,
                    RelativePath = instrumental.AudioFilePath,
                });
            }
        }
        return ustxProject;
    }

    private static UTrack GenerateTrack(Track osTrack) => new()
    {
        TrackName = osTrack.Title,
        Singer = "",
        Phonemizer = "OpenUtau.Core.DefaultPhonemizer",
        RendererSettings = new URendererSettings { Renderer = "CLASSIC" },
        Mute = osTrack.Mute,
        Solo = osTrack.Solo,
        Volume = osTrack.Volume > 0 ? Math.Log10(osTrack.Volume) * 10 : -60,
    };

    private UVoicePart GenerateVoicePart(SingingTrack osTrack, int trackNo)
    {
        var part = new UVoicePart { Name = osTrack.Title, TrackNo = trackNo, Position = 0, Notes = new List<UNote>() };
        if (osTrack.NoteList.Count == 0)
            return part;
        part.Notes.AddRange(GenerateNotes(osTrack.NoteList));
        part.Duration = Math.Max(osTrack.NoteList[^1].EndPos, 480);
        GeneratePitch(part, _ustxProject, osTrack.EditedParams.Pitch.Points, _firstBarLength);
        return part;
    }

    private static void GeneratePitch(
        UVoicePart part, USTXProject project, List<Point> osPitchSource, int firstBarLength)
    {
        const int pitchStart = BasePitchGenerator.PitchStart;
        const int pitchInterval = BasePitchGenerator.PitchInterval;
        var basePitch = new BasePitchGenerator(project).BasePitch(part);
        int pitchEndX = basePitch.Count * pitchInterval + firstBarLength + 1;

        var osPitch = new List<(int X, int Y)>();
        foreach (var point in osPitchSource)
            osPitch.Add((point.X, point.Y));

        if (osPitch.Count == 0)
        {
            osPitch.Add((0, -1));
            osPitch.Add((pitchEndX, -1));
        }
        if (osPitch[^1].X < pitchEndX)
        {
            osPitch.Add((osPitch[^1].X, -1));
            osPitch.Add((pitchEndX, -1));
        }

        int osPitchPointer = 0;
        var pitd = new UCurve { Abbr = "pitd", Xs = new List<int>(), Ys = new List<int>() };
        for (int i = 0; i < basePitch.Count; i++)
        {
            int time = i * pitchInterval + pitchStart;
            while (osPitch[osPitchPointer + 1].X <= time + firstBarLength)
                osPitchPointer++;
            if (osPitch[osPitchPointer].Y < 0)
            {
                pitd.Xs.Add(time);
                pitd.Ys.Add(0);
            }
            else if (osPitch[osPitchPointer].X == osPitch[osPitchPointer + 1].X)
            {
                pitd.Xs.Add(time);
                pitd.Ys.Add((int)Math.Round(
                    (osPitch[osPitchPointer].Y + osPitch[osPitchPointer + 1].Y) / 2.0
                    - (int)basePitch[i],
                    MidpointRounding.ToEven));
            }
            else
            {
                int x1 = osPitch[osPitchPointer].X - firstBarLength;
                int x2 = osPitch[osPitchPointer + 1].X - firstBarLength;
                int y1 = osPitch[osPitchPointer].Y;
                int y2 = osPitch[osPitchPointer + 1].Y;
                pitd.Xs.Add(time);
                pitd.Ys.Add((int)Math.Round(
                    (double)(y2 - y1) * (time - x1) / (x2 - x1) + y1 - (int)basePitch[i],
                    MidpointRounding.ToEven));
            }
        }
        if (!pitd.IsEmpty)
            part.Curves.Add(pitd);
    }

    private List<UNote> GenerateNotes(List<Note> osNotes)
    {
        var notes = new List<UNote>();
        int lastEnd = -480;
        int lastKey = 60;
        for (int i = 0; i < osNotes.Count; i++)
        {
            var osNote = osNotes[i];
            bool snapFirst = lastEnd >= osNote.StartPos;
            double y0 = snapFirst ? (lastKey - osNote.KeyNumber) * 10 : 0;
            string lyric = !string.IsNullOrEmpty(osNote.Pronunciation)
                ? osNote.Pronunciation
                : SubtitleSupport.SymbolPattern.Replace(osNote.Lyric, "");
            if (lyric == "-")
            {
                lyric = "+";
            }
            else if (lyric == "+")
            {
                for (int j = i - 1; j > 0; j--)
                {
                    if (osNotes[j].Lyric == "-")
                        notes[j].Lyric = "+~";
                    else
                        break;
                }
            }
            notes.Add(new UNote
            {
                Position = osNote.StartPos,
                Duration = osNote.Length,
                Tone = osNote.KeyNumber,
                Lyric = string.IsNullOrEmpty(lyric) ? osNote.Lyric : lyric,
                Pitch = new UPitch
                {
                    SnapFirst = snapFirst,
                    Data = new List<PitchPoint>
                    {
                        new() { X = -40, Y = y0, Shape = "io" },
                        new() { X = 0, Y = 0, Shape = "io" },
                    },
                },
                Vibrato = new UVibrato { Length = 0, Period = 175, Depth = 25, InValue = 10, Out = 10 },
            });
            lastEnd = osNote.EndPos;
            lastKey = osNote.KeyNumber;
        }
        return notes;
    }
}
