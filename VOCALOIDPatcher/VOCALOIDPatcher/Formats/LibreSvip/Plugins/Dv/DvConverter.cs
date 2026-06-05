using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;

public sealed class DvConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    private int _tickPrefix;
    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public override Project Load(byte[] content)
    {
        var dvProject = DvBinary.Parse(content);
        var timeSignatures = ParseTimeSignatures(dvProject.InnerProject.TimeSignatures);
        _firstBarLength = (int)Math.Round(timeSignatures.Count > 0
            ? timeSignatures[0].BarLength()
            : new TimeSignature().BarLength());
        var tempos = ParseTempos(dvProject.InnerProject.Tempos);

        var instrumentalTracks = ParseInstrumentalTracks(
            dvProject.InnerProject.Tracks
                .Where(t => t.TrackType == DvTrackType.Audio && t.AudioTrack != null && t.AudioTrack.Infos.Count > 0)
                .Select(t => t.AudioTrack!)
                .ToList());
        var singingTracks = ParseSingingTracks(
            dvProject.InnerProject.Tracks
                .Where(t => t.TrackType == DvTrackType.Singing && t.SingingTrack != null)
                .Select(t => t.SingingTrack!)
                .ToList(),
            tempos);

        var trackList = new List<Track>();
        trackList.AddRange(singingTracks);
        trackList.AddRange(instrumentalTracks);
        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = trackList,
        };
    }

    private List<TimeSignature> ParseTimeSignatures(List<DvTimeSignature> dvTimeSignatures)
    {
        _tickPrefix = 0;
        var timeSignatures = dvTimeSignatures
            .Select(ts => new TimeSignature(ts.MeasurePosition, ts.Numerator, ts.Denominator))
            .ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        int index = Math.Max(
            Search.FindLastIndex(timeSignatures, beat => beat.BarIndex <= 1),
            0);
        for (int i = 0; i <= index; i++)
        {
            if (i < index)
                _tickPrefix += (timeSignatures[i + 1].BarIndex - timeSignatures[i].BarIndex)
                    * (int)Math.Round(timeSignatures[i].BarLength());
            else
                _tickPrefix += (1 - timeSignatures[i].BarIndex)
                    * (int)Math.Round(timeSignatures[i].BarLength());
        }
        return TickCounter.SkipBeatList(timeSignatures, 1);
    }

    private List<SongTempo> ParseTempos(List<DvTempo> dvTempos)
    {
        var shifted = TickCounter.ShiftTempoList(
            dvTempos.Select(t => new SongTempo(t.Position, t.Bpm / 100.0)).ToList(),
            -_tickPrefix + _firstBarLength);
        var result = new List<SongTempo>();
        for (int i = 0; i < shifted.Count; i++)
        {
            var songTempo = i > 0 ? shifted[i] : new SongTempo(0, shifted[i].Bpm);
            if (i == 0 || songTempo.Position >= 0)
                result.Add(songTempo);
        }
        if (result.Count == 0)
            result.Add(new SongTempo());
        return result;
    }

    private List<InstrumentalTrack> ParseInstrumentalTracks(List<DvAudioTrack> dvAudioTracks)
    {
        var trackList = new List<InstrumentalTrack>();
        if (!ImportInstrumentalTrack)
            return trackList;
        foreach (var dvTrack in dvAudioTracks)
        {
            trackList.Add(new InstrumentalTrack
            {
                Title = !string.IsNullOrEmpty(dvTrack.Name) ? dvTrack.Name : dvTrack.Infos[0].Name,
                Mute = dvTrack.Mute != 0,
                Solo = dvTrack.Solo != 0,
                Offset = dvTrack.Infos[0].Start + _tickPrefix,
                AudioFilePath = dvTrack.Infos[0].Path,
            });
        }
        return trackList;
    }

    private List<SingingTrack> ParseSingingTracks(List<DvSingingTrack> dvSingingTracks, List<SongTempo> tempoList)
    {
        var trackList = new List<SingingTrack>();
        foreach (var dvTrack in dvSingingTracks)
        {
            int i = 0;
            foreach (var segment in dvTrack.Segments)
            {
                i++;
                var noteWithPitch = new List<DvNoteWithPitch>();
                int tickOffset = segment.Start;
                var track = new SingingTrack
                {
                    Title = $"{dvTrack.Name} {i}",
                    Mute = dvTrack.Mute != 0,
                    Solo = dvTrack.Solo != 0,
                    AiSingerName = segment.SingerName,
                    NoteList = ParseNotes(segment.Notes, noteWithPitch, tickOffset - _tickPrefix),
                };
                if (ImportPitch)
                {
                    var pitch = DvPitch.PitchFromDvTrack(
                        _firstBarLength,
                        new List<DvSegmentPitchRawData>
                        {
                            new DvSegmentPitchRawData(tickOffset - _tickPrefix, segment.PitchData),
                        },
                        noteWithPitch,
                        tempoList);
                    if (pitch != null)
                        track.EditedParams.Pitch = pitch;
                }
                trackList.Add(track);
            }
        }
        return trackList;
    }

    private List<Note> ParseNotes(List<DvNote> dvNotes, List<DvNoteWithPitch> noteWithPitch, int tickOffset)
    {
        var noteList = new List<Note>();
        foreach (var dvNote in dvNotes)
        {
            var note = new Note
            {
                StartPos = tickOffset + dvNote.Start,
                Length = dvNote.Length,
                KeyNumber = DvPitch.ConvertNoteKeyInt(dvNote.Key),
                Lyric = dvNote.Word,
                Pronunciation = dvNote.Phoneme != "-" ? dvNote.Phoneme : null,
            };
            noteWithPitch.Add(new DvNoteWithPitch
            {
                Note = note,
                BenDep = dvNote.BenDepth ?? 0,
                BenLen = dvNote.BenLength ?? 0,
                PorHead = dvNote.PorHead ?? 0,
                PorTail = dvNote.PorTail ?? 0,
                Vibrato = dvNote.NoteVibratoData.VibratoPoints,
            });
            noteList.Add(note);
        }
        return noteList;
    }

    public override byte[] Dump(Project project)
    {
        var timeSignatures = project.TimeSignatureList.Count > 0
            ? project.TimeSignatureList
            : new List<TimeSignature> { new() };
        _firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());
        _tickPrefix = (int)Math.Round(4 * timeSignatures[0].BarLength());
        var tempoList = project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new() };
        _synchronizer = new TimeSynchronizer(tempoList);

        var singingTracks = GenerateSingingTracks(project.TrackList.OfType<SingingTrack>().ToList());
        var audioTracks = GenerateInstrumentalTracks(project.TrackList.OfType<InstrumentalTrack>().ToList());

        var tracks = new List<DvTrack>();
        tracks.AddRange(singingTracks);
        tracks.AddRange(audioTracks);

        var dvProject = new DvProject
        {
            Version = 5,
            InnerProject = new DvInnerProject
            {
                Features = new HashSet<string> { "ext1", "ext2", "ext3", "ext4", "ext5", "ext6", "ext7" },
                Tracks = tracks,
                Tempos = GenerateTempos(tempoList),
                TimeSignatures = GenerateTimeSignatures(timeSignatures),
            },
        };
        return DvBinary.Build(dvProject);
    }

    private List<DvTimeSignature> GenerateTimeSignatures(List<TimeSignature> timeSignatures)
    {
        var adjusted = new List<TimeSignature>();
        for (int i = 0; i < timeSignatures.Count; i++)
        {
            var ts = timeSignatures[i];
            adjusted.Add(i > 0
                ? new TimeSignature(ts.BarIndex, ts.Numerator, ts.Denominator)
                : new TimeSignature(-3, ts.Numerator, ts.Denominator));
        }
        return TickCounter.ShiftBeatList(adjusted, 1)
            .Select(ts => new DvTimeSignature
            {
                MeasurePosition = ts.BarIndex,
                Numerator = ts.Numerator,
                Denominator = ts.Denominator,
            })
            .ToList();
    }

    private List<DvTempo> GenerateTempos(List<SongTempo> tempos)
    {
        var adjusted = new List<SongTempo>();
        for (int i = 0; i < tempos.Count; i++)
        {
            adjusted.Add(i > 0
                ? new SongTempo(tempos[i].Position, tempos[i].Bpm)
                : new SongTempo(0, tempos[i].Bpm));
        }
        return TickCounter.ShiftTempoList(adjusted, _tickPrefix - _firstBarLength)
            .Select(tempo => new DvTempo
            {
                Position = tempo.Position,
                Bpm = (int)Math.Round(tempo.Bpm * 100),
            })
            .ToList();
    }

    private List<DvTrack> GenerateInstrumentalTracks(List<InstrumentalTrack> instrumentalTracks)
    {
        var trackList = new List<DvTrack>();
        foreach (var track in instrumentalTracks)
        {
            double? duration = DvAudio.GetDurationSecs(track.AudioFilePath);
            if (duration == null)
                continue;
            var audioInfo = new DvAudioInfo
            {
                Path = track.AudioFilePath,
                Name = track.Title,
                Start = track.Offset + _tickPrefix,
                Length = (int)Math.Round(
                    _synchronizer.GetActualTicksFromSecsOffset(track.Offset, duration.Value)),
            };
            var dvTrack = new DvAudioTrack
            {
                Name = track.Title,
                Mute = (byte)(track.Mute ? 1 : 0),
                Solo = (byte)(track.Solo ? 1 : 0),
                Volume = DvConstants.DefaultVolume,
                Balance = 0,
                Infos = new List<DvAudioInfo> { audioInfo },
            };
            trackList.Add(new DvTrack
            {
                TrackType = DvTrackType.Audio,
                AudioTrack = dvTrack,
            });
        }
        return trackList;
    }

    private List<DvTrack> GenerateSingingTracks(List<SingingTrack> singingTracks)
    {
        var trackList = new List<DvTrack>();
        foreach (var track in singingTracks)
        {
            var dvNotes = GenerateNotes(track.NoteList);
            var dvSegment = new DvSegment
            {
                Start = _tickPrefix,
                Name = track.Title,
                SingerName = track.AiSingerName,
                Length = track.NoteList.Count > 0
                    ? track.NoteList.Max(n => n.EndPos)
                    : DvConstants.MinSegmentLength,
                Notes = dvNotes,
                VolumeData = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
                PitchData = new List<DvPoint> { new DvPoint(-1, -1), new DvPoint(307201, -1) },
                BreathData = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
                Ext3Data = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
                Ext5Data = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
                Ext6Data = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
                Ext7Data = new List<DvPoint> { new DvPoint(-1, 128), new DvPoint(307201, 128) },
            };
            var pitchRaw = DvPitch.GenerateForDv(_firstBarLength, track.EditedParams.Pitch, track.NoteList);
            if (pitchRaw != null)
                dvSegment.PitchData = pitchRaw.Data;
            var dvTrack = new DvSingingTrack
            {
                Name = track.Title,
                Mute = (byte)(track.Mute ? 1 : 0),
                Solo = (byte)(track.Solo ? 1 : 0),
                Volume = DvConstants.DefaultVolume,
                Balance = 0,
                Segments = new List<DvSegment> { dvSegment },
            };
            trackList.Add(new DvTrack
            {
                TrackType = DvTrackType.Singing,
                SingingTrack = dvTrack,
            });
        }
        return trackList;
    }

    private List<DvNote> GenerateNotes(List<Note> notes)
    {
        return notes.Select(note => new DvNote
        {
            Start = note.StartPos,
            Length = note.Length,
            Key = DvPitch.ConvertNoteKeyInt(note.KeyNumber),
            Phoneme = !string.IsNullOrEmpty(note.Pronunciation) ? note.Pronunciation! : note.Lyric,
            Word = note.Lyric,
            Padding1 = 0,
            Vibrato = 50,
            NoteVibratoData = new DvNoteParameter
            {
                AmplitudePoints = new List<DvPoint> { new DvPoint(-1, 0), new DvPoint(100001, 0) },
                FrequencyPoints = new List<DvPoint> { new DvPoint(-1, 0), new DvPoint(100001, 0) },
                VibratoPoints = new List<DvPoint> { new DvPoint(0, 0), new DvPoint(1124, 0) },
            },
            Phonemes = new DvPhoneme
            {
                Unknown1 = 0,
                ConsonantRate = 1.0f,
                VowelModified = 0,
                Medial = 1.0f,
                Rime = 1.0f,
                Ending = 1.0f,
            },
            BenDepth = 0,
            BenLength = 0,
            PorHead = 0,
            PorTail = 0,
            Timbre = -1,
            CrossLyric = "",
            CrossTimbre = -1,
            Unknown = DvConstants.NoteUnknownDataBlock.ToList(),
        }).ToList();
    }
}
