using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ppsf;

public sealed class PpsfParser
{
    private readonly bool _importPitch;
    private readonly bool _importInstrumental;
    private int _firstBarTicks;

    public PpsfParser(bool importPitch, bool importInstrumental)
    {
        _importPitch = importPitch;
        _importInstrumental = importInstrumental;
    }

    public Project ParseProject(PpsfProject ppsf)
    {
        var timeSignatures = ParseTimeSignatures(ppsf.Ppsf.Project.Meter);
        _firstBarTicks = (int)Math.Round(timeSignatures[0].BarLength());
        var tempos = ParseTempos(ppsf.Ppsf.Project.Tempo);
        var tracks = new List<Track>();
        var singingTracks = ParseSingingTracks(
            ppsf.Ppsf.Project.DvlTrack,
            ppsf.Ppsf.GuiSettings.TrackEditor.EventTracks);
        tracks.AddRange(singingTracks);
        if (_importInstrumental)
            tracks.AddRange(ParseInstrumentalTracks(ppsf.Ppsf.Project.AudioTrack));
        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = tracks,
        };
    }

    private static List<TimeSignature> ParseTimeSignatures(PpsfMeters meters)
    {
        var result = new List<TimeSignature>();
        var first = new TimeSignature(0, meters.Const.Nume, meters.Const.Denomi);
        if (meters.UseSequence)
        {
            foreach (var m in meters.Sequence)
                result.Add(new TimeSignature(m.Measure, m.Nume, m.Denomi));
        }
        if (result.Count == 0 || result[0].BarIndex != 0)
            result.Insert(0, first);
        return result;
    }

    private static List<SongTempo> ParseTempos(PpsfTempos tempos)
    {
        var result = new List<SongTempo>();
        var first = new SongTempo(0, tempos.Const / 10000.0);
        if (tempos.UseSequence)
        {
            foreach (var t in tempos.Sequence)
                result.Add(new SongTempo(t.Tick, t.Value / 10000.0));
        }
        if (result.Count == 0 || result[0].Position != 0)
            result.Insert(0, first);
        return result;
    }

    private List<Track> ParseInstrumentalTracks(List<PpsfAudioTrackItem> audioTracks)
    {
        var result = new List<Track>();
        foreach (var track in audioTracks)
        {
            for (int i = 0; i < track.Events.Count; i++)
            {
                var ev = track.Events[i];
                result.Add(new InstrumentalTrack
                {
                    Title = $"{track.Name} {i + 1}",
                    AudioFilePath = ev.FileAudioData.FilePath,
                    Offset = ev.TickPos,
                });
            }
        }
        return result;
    }

    private List<Track> ParseSingingTracks(
        List<PpsfDvlTrackItem>? dvlTracks,
        List<PpsfEventTrack> eventTracks)
    {
        var result = new List<Track>();
        if (dvlTracks == null)
            return result;
        int count = Math.Min(dvlTracks.Count, eventTracks.Count);
        for (int i = 0; i < count; i++)
        {
            var dvl = dvlTracks[i];
            var evt = eventTracks[i];
            var singing = new SingingTrack
            {
                Title = dvl.Name,
                AiSingerName = dvl.Singer.SingerName,
                Solo = evt.MuteSolo > 0,
                Mute = evt.MuteSolo < 0,
                NoteList = ParseNotes(dvl.Events),
            };
            if (_importPitch)
            {
                var keyIntervalDict = BuildKeyIntervalDict(dvl.Events, evt.Notes);
                foreach (var param in dvl.Parameters)
                {
                    if (param.BaseSequence == null || param.BaseSequence.Name != "pitch_bend")
                        continue;
                    foreach (var point in param.BaseSequence.Sequence)
                    {
                        if (point.CurveType == PpsfCurveType.Border && point.Value == 0)
                        {
                            singing.EditedParams.Pitch.Points.Add(new Point(
                                point.Pos + _firstBarTicks,
                                -100));
                        }
                        else
                        {
                            double? baseKey = keyIntervalDict.Get(point.Pos);
                            if (baseKey.HasValue)
                            {
                                singing.EditedParams.Pitch.Points.Add(new Point(
                                    point.Pos + _firstBarTicks,
                                    point.Value + (int)Math.Round(baseKey.Value * 100)));
                            }
                        }
                    }
                }
            }
            result.Add(singing);
        }
        return result;
    }

    private static List<Note> ParseNotes(List<PpsfDvlTrackEvent> events)
    {
        var result = new List<Note>();
        foreach (var ev in events)
        {
            if (ev.Enabled == false)
                continue;
            string lyric = IsLegatoChar(ev.Lyric) ? "-" : ev.Lyric;
            result.Add(new Note
            {
                KeyNumber = ev.NoteNumber,
                StartPos = ev.Pos,
                Length = ev.Length,
                Lyric = lyric,
                Pronunciation = ev.Symbols,
            });
        }
        return result;
    }

    private static bool IsLegatoChar(string lyric) =>
        lyric == "ー" || lyric == "−" || lyric == "-";

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
            var prevNote = notes.Count > i ? notes[i] : null;
            var nextNote = notes.Count > i + 1 ? notes[i + 1] : null;
            int prevPortOff = prevEv.PortamentoOffset;
            int prevPortLen = prevEv.PortamentoLength;
            int nextPortOff = nextEv.PortamentoOffset;
            int nextPortLen = nextEv.PortamentoLength;

            if (isFirst)
            {
                double endOfPrevConstant = prevEv.Pos + prevPortOff + prevPortLen;
                dict.SetConstant(0, endOfPrevConstant, prevEv.NoteNumber);
            }

            if (nextPortLen != 0)
            {
                double prevPortEnd = prevEv.Pos + prevPortOff + prevPortLen;
                double nextPortStart = nextEv.Pos + nextPortOff;
                if (prevPortEnd < nextPortStart)
                    dict.SetConstant(prevPortEnd, nextPortStart, prevEv.NoteNumber);

                double nextPortEnd = nextEv.Pos + nextPortOff + nextPortLen;
                double startY = prevEv.NoteNumber;
                double endY = nextEv.NoteNumber;
                double startX = nextPortStart;
                double endX = nextPortEnd;
                dict.Set(nextPortStart, nextPortEnd,
                    x => MusicMath.CosineEasingInOutInterpolation(x, (startX, startY), (endX, endY)));
            }
            else
            {
                double prevPortEnd = prevEv.Pos + prevPortOff + prevPortLen;
                double prevNoteEnd = prevEv.EndPos;
                if (prevPortEnd < prevNoteEnd)
                    dict.SetConstant(prevPortEnd, prevNoteEnd, prevEv.NoteNumber);
            }

            if (isLast)
            {
                double nextPortEnd = nextEv.Pos + nextPortOff + nextPortLen;
                dict.SetConstant(nextPortEnd, double.MaxValue / 2, nextEv.NoteNumber);
            }
        }
        return dict;
    }
}
