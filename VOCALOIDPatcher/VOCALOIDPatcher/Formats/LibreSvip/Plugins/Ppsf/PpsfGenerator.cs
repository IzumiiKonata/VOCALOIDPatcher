using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ppsf;

public sealed class PpsfGenerator
{
    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public PpsfProject GenerateProject(Project project)
    {
        _firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());
        _synchronizer = new TimeSynchronizer(project.SongTempoList);

        var ppsf = new PpsfProject();
        ppsf.Ppsf.Project.Meter = GenerateTimeSignatures(project.TimeSignatureList);
        ppsf.Ppsf.Project.Tempo = GenerateTempos(project.SongTempoList);
        ppsf.Ppsf.Project.DvlTrack = GenerateSingingTracks(
            project.TrackList,
            ppsf.Ppsf.GuiSettings.TrackEditor.EventTracks);
        ppsf.Ppsf.Project.AudioTrack = GenerateInstrumentalTracks(
            project.TrackList,
            ppsf.Ppsf.GuiSettings.TrackEditor.EventTracks);

        ppsf.Ppsf.GuiSettings.ProjectLength = _firstBarLength;
        foreach (var evt in ppsf.Ppsf.GuiSettings.TrackEditor.EventTracks)
        {
            foreach (var region in evt.Regions)
            {
                int candidate = region.Position + region.Length + _firstBarLength;
                if (candidate > ppsf.Ppsf.GuiSettings.ProjectLength)
                    ppsf.Ppsf.GuiSettings.ProjectLength = candidate;
            }
        }
        return ppsf;
    }

    private static PpsfMeters GenerateTimeSignatures(List<TimeSignature> timeSignatures)
    {
        var meters = new PpsfMeters();
        if (timeSignatures.Count > 0)
        {
            meters.Const.Nume = timeSignatures[0].Numerator;
            meters.Const.Denomi = timeSignatures[0].Denominator;
        }
        if (timeSignatures.Count > 1)
        {
            meters.UseSequence = true;
            meters.Sequence = timeSignatures.Skip(1).Select(ts => new PpsfMeter
            {
                Nume = ts.Numerator,
                Denomi = ts.Denominator,
                Measure = ts.BarIndex,
            }).ToList();
        }
        return meters;
    }

    private static PpsfTempos GenerateTempos(List<SongTempo> tempos)
    {
        var ppsf = new PpsfTempos();
        if (tempos.Count > 0)
            ppsf.Const = (int)Math.Round(tempos[0].Bpm * 10000);
        if (tempos.Count > 1)
        {
            ppsf.UseSequence = true;
            ppsf.Sequence = tempos.Skip(1).Select(t => new PpsfTempo
            {
                Value = (int)Math.Round(t.Bpm * 10000),
                Tick = t.Position,
                CurveType = PpsfCurveType.Border,
            }).ToList();
        }
        return ppsf;
    }

    private List<PpsfDvlTrackItem> GenerateSingingTracks(
        List<Track> tracks,
        List<PpsfEventTrack> eventTracks)
    {
        var dvlTracks = new List<PpsfDvlTrackItem>();
        foreach (var track in tracks)
        {
            if (track is not SingingTrack singing)
                continue;
            int muteFlag = singing.Mute ? -1 : singing.Solo ? 1 : 0;
            var eventTrack = new PpsfEventTrack
            {
                Index = eventTracks.Count,
                TrackType = 4,
                MuteSolo = muteFlag,
                Notes = new List<PpsfNote>(),
                NtEnvelopePresetId = 2,
            };
            var (trackEvents, ppsfNotes) = GenerateNotes(singing.NoteList);
            eventTrack.Notes = ppsfNotes;
            int regionLength = trackEvents.Count > 0
                ? trackEvents[^1].Pos + trackEvents[^1].Length
                : 0;
            eventTrack.Regions.Add(new PpsfRegion
            {
                AutoExpandLeft = true,
                AutoExpandRight = true,
                Position = 0,
                Length = regionLength,
            });
            var keyIntervalDict = BuildKeyIntervalDict(trackEvents, ppsfNotes);
            var (pitchParam, pitchCurvePoint) = GeneratePitch(
                singing.EditedParams.Pitch,
                keyIntervalDict,
                singing.NoteList);
            eventTrack.CurvePoints.Add(pitchCurvePoint);
            var dvl = new PpsfDvlTrackItem
            {
                Name = singing.Title,
                Events = trackEvents,
                Parameters = new List<PpsfSeqParam> { pitchParam },
            };
            dvlTracks.Add(dvl);
            eventTracks.Add(eventTrack);
        }
        return dvlTracks;
    }

    private (PpsfSeqParam, PpsfCurvePoint) GeneratePitch(
        ParamCurve pitch,
        PiecewiseIntervalDict keyIntervalDict,
        List<Note> notes)
    {
        var pitchParam = new PpsfSeqParam
        {
            BaseSequence = new PpsfBaseSequence
            {
                Constant = 0,
                Name = "pitch_bend",
                UseSequence = true,
            },
        };
        var curvePoint = new PpsfCurvePoint
        {
            SubTrackCategory = 7,
            SubTrackId = 2,
        };

        bool inPitchPart = false;
        foreach (var point in pitch.Points)
        {
            int pos = point.X - _firstBarLength;
            if (point.Y == -100)
            {
                if (point.X != Point.StartX && point.X != Point.EndX
                    && inPitchPart
                    && pitchParam.BaseSequence.Sequence.Count > 0)
                {
                    var last = pitchParam.BaseSequence.Sequence[^1];
                    pitchParam.BaseSequence.Sequence.Add(new PpsfParamPoint
                    {
                        CurveType = PpsfCurveType.Border,
                        Pos = last.Pos,
                        Value = last.Value,
                    });
                    curvePoint.Sequence.Add(new PpsfCurvePointSeq
                    {
                        BorderType = 7,
                        AbsValue = -1,
                        EditedByUser = true,
                        RegionIndex = -1,
                        NoteIndex = -1,
                        SegArrayId = -1,
                    });
                    inPitchPart = false;
                }
            }
            else
            {
                double? baseKey = keyIntervalDict.Get(pos);
                if (baseKey.HasValue)
                {
                    if (!inPitchPart)
                    {
                        pitchParam.BaseSequence.Sequence.Add(new PpsfParamPoint
                        {
                            CurveType = PpsfCurveType.Border,
                            Pos = pos - 1,
                            Value = 0,
                        });
                        curvePoint.Sequence.Add(new PpsfCurvePointSeq
                        {
                            BorderType = 6,
                            AbsValue = -1,
                            EditedByUser = true,
                            RegionIndex = -1,
                            NoteIndex = -1,
                            SegArrayId = -1,
                        });
                        inPitchPart = true;
                    }
                    pitchParam.BaseSequence.Sequence.Add(new PpsfParamPoint
                    {
                        CurveType = PpsfCurveType.Normal,
                        Pos = pos,
                        Value = point.Y - (int)Math.Round(baseKey.Value * 100),
                    });
                    int noteIdx = Search.FindIndex(
                        notes,
                        n => n.StartPos <= pos && pos <= n.EndPos);
                    curvePoint.Sequence.Add(new PpsfCurvePointSeq
                    {
                        BorderType = 1,
                        AbsValue = point.Y,
                        EditedByUser = true,
                        RegionIndex = -1,
                        NoteIndex = noteIdx,
                        SegArrayId = -1,
                    });
                }
            }
        }
        return (pitchParam, curvePoint);
    }

    private List<PpsfAudioTrackItem> GenerateInstrumentalTracks(
        List<Track> tracks,
        List<PpsfEventTrack> eventTracks)
    {
        var result = new List<PpsfAudioTrackItem>();
        foreach (var track in tracks)
        {
            if (track is not InstrumentalTrack instrumental)
                continue;
            double offsetSecs = _synchronizer.GetActualSecsFromTicks(instrumental.Offset);
            int tickLength = 0;
            var audioItem = new PpsfAudioTrackItem
            {
                Name = instrumental.Title,
                Events = new List<PpsfAudioTrackEvent>
                {
                    new()
                    {
                        TickPos = instrumental.Offset,
                        TickLength = tickLength,
                        FileAudioData = new PpsfFileAudioData { FilePath = instrumental.AudioFilePath },
                    },
                },
            };
            result.Add(audioItem);
            int muteFlag = instrumental.Mute ? -1 : instrumental.Solo ? 1 : 0;
            eventTracks.Add(new PpsfEventTrack
            {
                Index = eventTracks.Count,
                TrackType = 1,
                MuteSolo = muteFlag,
                Regions = new List<PpsfRegion>
                {
                    new()
                    {
                        Length = tickLength,
                        Position = instrumental.Offset,
                        AudioEventIndex = 0,
                    },
                },
            });
        }
        return result;
    }

    private static (List<PpsfDvlTrackEvent>, List<PpsfNote>) GenerateNotes(List<Note> notes)
    {
        var events = new List<PpsfDvlTrackEvent>();
        var ppsfNotes = new List<PpsfNote>();
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            bool isLegato = note.Lyric == "ー" || note.Lyric == "−" || note.Lyric == "-";
            string symbols = isLegato ? "-" : (note.Pronunciation ?? "4 a");
            events.Add(new PpsfDvlTrackEvent
            {
                NoteNumber = note.KeyNumber,
                Pos = note.StartPos,
                Length = note.Length,
                Lyric = note.Lyric,
                Symbols = symbols,
            });
            ppsfNotes.Add(new PpsfNote
            {
                RegionIndex = 0,
                EventIndex = i,
                Length = note.Length,
                Syllables = new List<PpsfSyllable>
                {
                    new()
                    {
                        LyricText = note.Lyric,
                        SymbolsText = symbols,
                    },
                },
            });
        }
        return (events, ppsfNotes);
    }

    private static PiecewiseIntervalDict BuildKeyIntervalDict(
        List<PpsfDvlTrackEvent> events,
        List<PpsfNote> notes)
    {
        var dict = new PiecewiseIntervalDict();
        if (events.Count == 0)
            return dict;
        if (events.Count == 1)
        {
            dict.SetConstant(0, double.MaxValue / 2, events[0].NoteNumber);
            return dict;
        }
        for (int i = 0; i < events.Count - 1; i++)
        {
            bool isFirst = i == 0;
            bool isLast = i == events.Count - 2;
            var prevEv = events[i];
            var nextEv = events[i + 1];

            if (isFirst)
            {
                double endOfPrev = prevEv.Pos + prevEv.PortamentoOffset + prevEv.PortamentoLength;
                dict.SetConstant(0, endOfPrev, prevEv.NoteNumber);
            }

            if (nextEv.PortamentoLength != 0)
            {
                double prevPortEnd = prevEv.Pos + prevEv.PortamentoOffset + prevEv.PortamentoLength;
                double nextPortStart = nextEv.Pos + nextEv.PortamentoOffset;
                if (prevPortEnd < nextPortStart)
                    dict.SetConstant(prevPortEnd, nextPortStart, prevEv.NoteNumber);

                double nextPortEnd = nextEv.Pos + nextEv.PortamentoOffset + nextEv.PortamentoLength;
                double startY = prevEv.NoteNumber;
                double endY = nextEv.NoteNumber;
                double startX = nextPortStart;
                double endX = nextPortEnd;
                dict.Set(nextPortStart, nextPortEnd,
                    x => MusicMath.CosineEasingInOutInterpolation(x, (startX, startY), (endX, endY)));
            }
            else
            {
                double prevPortEnd = prevEv.Pos + prevEv.PortamentoOffset + prevEv.PortamentoLength;
                double prevNoteEnd = prevEv.EndPos;
                if (prevPortEnd < prevNoteEnd)
                    dict.SetConstant(prevPortEnd, prevNoteEnd, prevEv.NoteNumber);
            }

            if (isLast)
            {
                double nextPortEnd = nextEv.Pos + nextEv.PortamentoOffset + nextEv.PortamentoLength;
                dict.SetConstant(nextPortEnd, double.MaxValue / 2, nextEv.NoteNumber);
            }
        }
        return dict;
    }
}
