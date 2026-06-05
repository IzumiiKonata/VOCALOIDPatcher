using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;

internal sealed class CcsGenerator
{
    private const double TickRate = 2.0;
    private const int OctaveOffset = -1;
    private const string DefaultPhoneme = "r,a";

    private readonly string _defaultSingerName;
    private readonly string _defaultSingerId;
    private readonly string _defaultSingerVersion;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private int _firstBarLength;

    public CcsGenerator(string defaultSingerName, string defaultSingerId, string defaultSingerVersion)
    {
        _defaultSingerName = defaultSingerName;
        _defaultSingerId = defaultSingerId;
        _defaultSingerVersion = defaultSingerVersion;
    }

    public CcsProject GenerateProject(Project project)
    {
        _synchronizer = new TimeSynchronizer(project.SongTempoList);
        _firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());

        var ccsProject = new CcsProject();
        ccsProject.Generation.Svss.SoundSources.Add(new CcsSoundSource
        {
            Id = _defaultSingerId,
            Name = _defaultSingerName,
            Version = _defaultSingerVersion,
        });

        var defaultTempo = GenerateTempos(project.SongTempoList);
        var defaultBeat = GenerateBeat(project.TimeSignatureList);

        foreach (var (unit, group) in GenerateTracks(project.TrackList, defaultTempo, defaultBeat, project.SongTempoList))
        {
            ccsProject.Sequence.Scene.Units.Add(unit);
            ccsProject.Sequence.Scene.Groups.Add(group);
        }

        return ccsProject;
    }

    private CcsTempo GenerateTempos(List<SongTempo> tempos)
    {
        var result = new CcsTempo();
        result.Sounds.Add(new CcsSound { Clock = 0, Tempo = tempos[0].Bpm });
        foreach (var t in tempos.Skip(1))
            result.Sounds.Add(new CcsSound { Clock = (int)(t.Position * TickRate), Tempo = t.Bpm });
        return result;
    }

    private CcsBeat GenerateBeat(List<TimeSignature> timeSigs)
    {
        var beat = new CcsBeat();
        beat.Times.Add(new CcsTime
        {
            Clock = 0,
            Beats = timeSigs[0].Numerator,
            BeatType = timeSigs[0].Denominator,
        });

        double tick = 0.0;
        var prevTs = timeSigs[0];
        foreach (var ts in timeSigs.Skip(1))
        {
            if (ts.BarIndex > prevTs.BarIndex)
                tick += (ts.BarIndex - prevTs.BarIndex) * prevTs.BarLength();
            beat.Times.Add(new CcsTime
            {
                Clock = (int)(tick * TickRate),
                Beats = ts.Numerator,
                BeatType = ts.Denominator,
            });
            prevTs = ts;
        }
        return beat;
    }

    private IEnumerable<(CcsUnit Unit, CcsGroup Group)> GenerateTracks(
        List<Track> tracks,
        CcsTempo tempo,
        CcsBeat beat,
        List<SongTempo> tempoList)
    {
        foreach (var track in tracks)
        {
            if (track is SingingTrack singingTrack)
            {
                var groupId = Guid.NewGuid().ToString("D");
                var group = new CcsGroup
                {
                    Id = groupId,
                    Name = singingTrack.Title,
                    IsMuted = singingTrack.Mute,
                    IsSolo = singingTrack.Solo,
                    CastId = _defaultSingerId,
                };

                var song = new CcsSong
                {
                    Tempo = tempo,
                    Beat = beat,
                };
                song.Score.Notes = GenerateNotes(singingTrack.NoteList);

                string? durationStr = null;
                if (singingTrack.NoteList.Count > 0)
                {
                    int maxTick = singingTrack.NoteList[^1].EndPos;
                    double maxSecs = _synchronizer.GetActualSecsFromTicks(maxTick);
                    durationStr = SecsToXmlTime(maxSecs);
                }

                var logF0 = GeneratePitch(singingTrack.EditedParams.Pitch, tempoList);
                if (logF0 != null)
                    song.Parameter.LogF0 = logF0;

                var unit = new CcsUnit
                {
                    Id = "",
                    Group = groupId,
                    StartTime = "00:00:00.000",
                    Duration = durationStr,
                    Category = "SingerSong",
                    CastId = _defaultSingerId,
                    Song = song,
                };
                yield return (unit, group);
            }
            else if (track is InstrumentalTrack instTrack)
            {
                if (string.IsNullOrEmpty(instTrack.AudioFilePath)) continue;
                double startSecs = _synchronizer.GetActualSecsFromTicks(instTrack.Offset);
                var groupId = Guid.NewGuid().ToString("D");
                var group = new CcsGroup
                {
                    Id = groupId,
                    Name = instTrack.Title,
                    IsMuted = instTrack.Mute,
                    IsSolo = instTrack.Solo,
                    Category = "OuterAudio",
                };
                var unit = new CcsUnit
                {
                    Id = "",
                    Group = groupId,
                    StartTime = SecsToXmlTime(startSecs),
                    Category = "OuterAudio",
                    FilePath = instTrack.AudioFilePath,
                };
                yield return (unit, group);
            }
        }
    }

    private List<CcsNote> GenerateNotes(List<Note> notes)
    {
        var result = new List<CcsNote>();
        foreach (var note in notes)
        {
            string lyric = note.Lyric == "-" ? "ー" : note.Lyric;
            string? phonetic = note.Pronunciation;
            if (phonetic == null && !IsKanaOrRomaji(lyric))
                phonetic = DefaultPhoneme;

            result.Add(new CcsNote
            {
                Clock = (int)(note.StartPos * TickRate),
                Duration = (int)(note.Length * TickRate),
                Lyric = lyric,
                Phonetic = phonetic,
                PitchOctave = note.KeyNumber / Constants.KeyInOctave + OctaveOffset,
                PitchStep = note.KeyNumber % Constants.KeyInOctave,
            });
        }
        return result;
    }

    private CcsParameter? GeneratePitch(ParamCurve pitch, List<SongTempo> tempoList)
    {
        var data = CcsPitchHelper.GenerateForCcs(pitch, tempoList, _firstBarLength);
        if (data == null) return null;

        var param = new CcsParameter { Length = data.Length };
        foreach (var ev in data.Events)
        {
            param.DataPoints.Add(new CcsDataPoint
            {
                IsNoData = false,
                Index = ev.Idx,
                Repeat = ev.Repeat,
                Value = ev.Value,
            });
        }
        return param;
    }

    private static string SecsToXmlTime(double secs)
    {
        var ts = TimeSpan.FromSeconds(secs);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static bool IsKanaOrRomaji(string lyric)
    {
        if (string.IsNullOrEmpty(lyric)) return false;
        foreach (char c in lyric)
        {
            bool isHiragana = c >= 'ぁ' && c <= 'ゖ';
            bool isKatakana = c >= 'ァ' && c <= 'ヶ';
            bool isRomaji = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            if (!isHiragana && !isKatakana && !isRomaji)
                return false;
        }
        return true;
    }
}
