using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;

internal sealed class CcsParser
{
    private const double TickRate = 2.0;
    private const int OctaveOffset = -1;

    private readonly bool _importPitch;
    private readonly bool _importInstrumental;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private readonly Dictionary<string, string> _singerIdToName = new();

    public CcsParser(bool importPitch, bool importInstrumental)
    {
        _importPitch = importPitch;
        _importInstrumental = importInstrumental;
    }

    public Project ParseProject(CcsProject ccsProject)
    {
        var scene = ccsProject.Sequence.Scene;

        foreach (var src in ccsProject.Generation.Svss.SoundSources)
            if (src.Id != null && src.Name != null)
                _singerIdToName[src.Id] = src.Name;

        var id2Group = scene.Groups
            .Where(g => g.Id != null)
            .ToDictionary(g => g.Id!);

        var allTempos = new List<SongTempo>();
        var allTimeSignatures = new List<TimeSignature>();
        var tracks = new List<Track>();

        var singingUnits = scene.Units.Where(u => u.Category == "SingerSong").ToList();
        var audioUnits = scene.Units.Where(u => u.Category == "OuterAudio").ToList();

        for (int i = 0; i < singingUnits.Count; i++)
        {
            var unit = singingUnits[i];
            id2Group.TryGetValue(unit.Group ?? "", out var group);
            group ??= new CcsGroup();

            var (track, tempos, timeSigs) = ParseSingingTrack(i, unit, group);
            tracks.Add(track);
            allTempos.AddRange(tempos);
            allTimeSignatures.AddRange(timeSigs);
        }

        var mergedTempos = MergeTempos(allTempos);
        var mergedTimeSigs = MergeTimeSignatures(allTimeSignatures);

        _synchronizer = new TimeSynchronizer(mergedTempos);

        if (_importInstrumental)
        {
            for (int i = 0; i < audioUnits.Count; i++)
            {
                var unit = audioUnits[i];
                id2Group.TryGetValue(unit.Group ?? "", out var group);
                group ??= new CcsGroup();
                tracks.Add(ParseInstrumentalTrack(i, unit, group));
            }
        }

        return new Project
        {
            SongTempoList = TickCounter.SkipTempoList(mergedTempos, 0),
            TimeSignatureList = TickCounter.SkipBeatList(mergedTimeSigs, 0),
            TrackList = tracks,
        };
    }

    private static List<SongTempo> MergeTempos(List<SongTempo> tempos)
    {
        if (tempos.Count == 0) return new List<SongTempo> { new() };
        var dict = new Dictionary<int, SongTempo>();
        foreach (var t in tempos)
            if (!dict.ContainsKey(t.Position))
                dict[t.Position] = t;
        return dict.Values.OrderBy(t => t.Position).ToList();
    }

    private static List<TimeSignature> MergeTimeSignatures(List<TimeSignature> timeSigs)
    {
        if (timeSigs.Count == 0) return new List<TimeSignature> { new() };
        var dict = new Dictionary<int, TimeSignature>();
        foreach (var ts in timeSigs)
            if (!dict.ContainsKey(ts.BarIndex))
                dict[ts.BarIndex] = ts;
        return dict.Values.OrderBy(ts => ts.BarIndex).ToList();
    }

    private InstrumentalTrack ParseInstrumentalTrack(int index, CcsUnit unit, CcsGroup group)
    {
        int offset = 0;
        if (unit.StartTime != null && TimeSpan.TryParse(unit.StartTime, out var startTs))
            offset = (int)_synchronizer.GetActualTicksFromSecs(startTs.TotalSeconds);

        return new InstrumentalTrack
        {
            Title = group.Name ?? $"Track {index + 1}",
            AudioFilePath = unit.FilePath ?? "",
            Mute = group.IsMuted,
            Solo = group.IsSolo,
            Offset = offset,
        };
    }

    private (SingingTrack Track, List<SongTempo> Tempos, List<TimeSignature> TimeSigs)
        ParseSingingTrack(int index, CcsUnit unit, CcsGroup group)
    {
        var song = unit.Song ?? new CcsSong();

        var timeNodes = song.Beat?.Times ?? new List<CcsTime>();
        double prevTick = 0.0;
        var timeSignatures = new List<TimeSignature>
        {
            new(0, 4, 4),
        };

        foreach (var timeNode in timeNodes)
        {
            double tick = timeNode.Clock / TickRate;
            int? numerator = timeNode.Beats;
            int? denominator = timeNode.BeatType;
            if (numerator == null || denominator == null) continue;

            double ticksInMeasure = timeSignatures[^1].BarLength();
            double tickDiff = tick - prevTick;
            double measureDiff = tickDiff / ticksInMeasure;
            timeSignatures.Add(new TimeSignature(
                (int)(timeSignatures[^1].BarIndex + measureDiff),
                numerator.Value,
                denominator.Value
            ));
            prevTick = tick;
        }

        timeSignatures = timeSignatures
            .Select(ts => new TimeSignature(ts.BarIndex - 4, ts.Numerator, ts.Denominator))
            .ToList();

        int tickPrefix = (int)timeSignatures[0].BarLength();

        var tempoNodes = song.Tempo?.Sounds ?? new List<CcsSound>();
        var tempos = tempoNodes
            .Where(s => s.Clock != null && s.Tempo != null)
            .Select(s => new SongTempo((int)(s.Clock!.Value / TickRate), s.Tempo!.Value))
            .ToList();

        var notes = new List<Note>();
        foreach (var noteNode in song.Score.Notes)
        {
            if (noteNode.Clock == null || noteNode.Duration == null ||
                noteNode.PitchStep == null || noteNode.PitchOctave == null ||
                noteNode.Lyric == null)
                continue;

            int tickOn = (int)(noteNode.Clock.Value / TickRate);
            int duration = (int)(noteNode.Duration.Value / TickRate);
            int key = noteNode.PitchStep.Value + (noteNode.PitchOctave.Value - OctaveOffset) * 12;
            string lyric = noteNode.Lyric;

            if (string.IsNullOrEmpty(lyric)) continue;

            if (lyric == "ー")
                lyric = "-";

            string? phoneme = null;
            if (noteNode.Phonetic != null)
                phoneme = noteNode.Phonetic.Replace(",", " ");

            notes.Add(new Note
            {
                StartPos = tickOn,
                Length = duration,
                KeyNumber = key,
                Lyric = lyric,
                Pronunciation = phoneme,
            });
        }

        CcsTrackPitchData? pitchData = null;
        if (_importPitch && song.Parameter.LogF0 != null)
        {
            var pitchEvents = ParseParamData(song.Parameter.LogF0);
            var vibAmpEvents = song.Parameter.VibAmp != null
                ? ParseParamData(song.Parameter.VibAmp)
                : new List<CcsParamEvent>();
            var vibFrqEvents = song.Parameter.VibFrq != null
                ? ParseParamData(song.Parameter.VibFrq)
                : new List<CcsParamEvent>();

            pitchData = new CcsTrackPitchData
            {
                Events = pitchEvents,
                Tempos = tempos,
                TickPrefix = tickPrefix,
                VibratoAmplitudeEvents = vibAmpEvents,
                VibratoFrequencyEvents = vibFrqEvents,
            };
        }

        var track = new SingingTrack
        {
            Title = group.Name ?? $"Track {index + 1}",
            AiSingerName = _singerIdToName.TryGetValue(unit.CastId ?? "", out var name) ? name : "",
            NoteList = notes,
            Mute = group.IsMuted,
            Solo = group.IsSolo,
        };

        if (pitchData != null)
        {
            var pitchCurve = CcsPitchHelper.PitchFromCcsTrack(pitchData);
            if (pitchCurve != null)
                track.EditedParams.Pitch = pitchCurve;
        }

        return (track, tempos, timeSignatures);
    }

    private static List<CcsParamEvent> ParseParamData(CcsParameter param)
    {
        var result = new List<CcsParamEvent>();
        foreach (var dp in param.DataPoints)
        {
            if (dp.IsNoData) continue;
            if (dp.Value == null) continue;
            result.Add(new CcsParamEvent(
                dp.Index == 0 ? null : dp.Index,
                dp.Repeat == 0 ? null : dp.Repeat,
                dp.Value.Value
            ));
        }
        return result;
    }
}
