using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tssln;

public sealed class TsslnConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumentalTrack { get; set; } = true;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    private const int SingingTrackType = 0;
    private const int AudioTrackType = 2;

    private TimeSynchronizer _timeSynchronizer = new(new List<SongTempo> { new() });
    private int _firstBarLength;

    public override Project Load(byte[] content)
    {
        var root = new TsslnTree(JuceBinary.Parse(content));
        var timeSignatures = new List<TimeSignature>();
        var tempos = new List<SongTempo>();
        var tracks = new List<Track>();

        var trackContainers = root.Children("Tracks");
        foreach (var container in trackContainers)
        {
            foreach (var item in container.Children("Track"))
            {
                int type = item.GetInt("Type") ?? SingingTrackType;
                if (type != SingingTrackType)
                    continue;
                var parsed = ParseSingingTrack(item);
                if (parsed == null)
                    continue;
                tracks.Add(parsed.Value.Track);
                tempos.AddRange(parsed.Value.Tempos);
                timeSignatures.AddRange(parsed.Value.TimeSignatures);
            }
        }

        tempos = MergeTempos(tempos);
        _timeSynchronizer = new TimeSynchronizer(tempos);

        if (ImportInstrumentalTrack)
        {
            foreach (var container in trackContainers)
            {
                foreach (var item in container.Children("Track"))
                {
                    int type = item.GetInt("Type") ?? SingingTrackType;
                    if (type != AudioTrackType)
                        continue;
                    var audioEvents = item.Children("AudioEvent");
                    int i = 1;
                    string name = item.GetString("Name") ?? "";
                    foreach (var ev in audioEvents)
                    {
                        string path = ev.GetString("Path") ?? "";
                        double offset = ev.GetDouble("Offset") ?? 0;
                        tracks.Add(new InstrumentalTrack
                        {
                            Title = $"{name} {i}",
                            AudioFilePath = path,
                            Offset = (int)_timeSynchronizer.GetActualTicksFromSecs(offset),
                        });
                        i++;
                    }
                }
            }
        }

        timeSignatures = MergeTimeSignatures(timeSignatures);

        return new Project
        {
            TimeSignatureList = TickCounter.SkipBeatList(timeSignatures, 0),
            SongTempoList = TickCounter.SkipTempoList(tempos, 0),
            TrackList = tracks,
        };
    }

    private static List<SongTempo> MergeTempos(List<SongTempo> tempos)
    {
        var seen = new HashSet<int>();
        var result = new List<SongTempo>();
        foreach (var tempo in tempos)
            if (seen.Add(tempo.Position))
                result.Add(tempo);
        return result.Count > 0 ? result : new List<SongTempo> { new() };
    }

    private static List<TimeSignature> MergeTimeSignatures(List<TimeSignature> timeSignatures)
    {
        var seen = new HashSet<int>();
        var result = new List<TimeSignature>();
        foreach (var ts in timeSignatures)
            if (seen.Add(ts.BarIndex))
                result.Add(ts);
        return result.Count > 0 ? result : new List<TimeSignature> { new() };
    }

    private (SingingTrack Track, List<SongTempo> Tempos, List<TimeSignature> TimeSignatures)? ParseSingingTrack(TsslnTree track)
    {
        var pluginData = track.GetBinaryTree("PluginData");
        if (pluginData == null)
            return null;
        var stateInfo = pluginData.FirstChild("StateInformation") ?? pluginData;
        var songs = stateInfo.Children("Song");
        if (songs.Count == 0)
            return null;

        var timeSignatures = new List<TimeSignature>
        {
            new(0, 4, 4),
        };
        int prevTick = 0;
        var tempos = new List<SongTempo>();
        var notes = new List<Note>();

        int tickPrefix = (int)timeSignatures[0].BarLength();

        foreach (var song in songs)
        {
            foreach (var beat in song.Children("Beat"))
            {
                foreach (var timeNode in beat.Children("Time"))
                {
                    int clock = timeNode.GetInt("Clock") ?? 0;
                    int tick = (int)(clock / TsslnConstants.TickRate);
                    int numerator = timeNode.GetInt("Beats") ?? 4;
                    int denominator = timeNode.GetInt("BeatType") ?? 4;
                    double ticksInMeasure = timeSignatures[^1].BarLength();
                    int tickDiff = tick - prevTick;
                    double measureDiff = tickDiff / ticksInMeasure;
                    timeSignatures.Add(new TimeSignature(
                        (int)(timeSignatures[^1].BarIndex + measureDiff),
                        numerator,
                        denominator));
                    prevTick = tick;
                }
            }
            foreach (var tempo in song.Children("Tempo"))
            {
                foreach (var tempoNode in tempo.Children("Sound"))
                {
                    int clock = tempoNode.GetInt("Clock") ?? 0;
                    int tick = (int)(clock / TsslnConstants.TickRate);
                    double bpm = tempoNode.GetDouble("Tempo") ?? Constants.DefaultBpm;
                    tempos.Add(new SongTempo(tick, bpm));
                }
            }
            foreach (var score in song.Children("Score"))
            {
                foreach (var noteNode in score.Children("Note"))
                {
                    int clock = noteNode.GetInt("Clock") ?? 0;
                    int duration = noteNode.GetInt("Duration") ?? 0;
                    int pitchStep = noteNode.GetInt("PitchStep") ?? 0;
                    int pitchOctave = (noteNode.GetInt("PitchOctave") ?? 0) - TsslnConstants.OctaveOffset;
                    string lyric = noteNode.GetString("Lyric") ?? "";
                    string? phoneme = noteNode.GetString("Phoneme");
                    if (!string.IsNullOrEmpty(phoneme))
                        phoneme = phoneme!.Replace(",", " ");
                    else
                        phoneme = null;
                    notes.Add(new Note
                    {
                        KeyNumber = pitchStep + pitchOctave * 12,
                        Lyric = lyric == char.ConvertFromUtf32(TsslnConstants.ProlongedSoundMark) ? "-" : lyric,
                        StartPos = (int)(clock / TsslnConstants.TickRate),
                        Length = (int)(duration / TsslnConstants.TickRate),
                        Pronunciation = phoneme,
                    });
                }
            }
        }

        tempos = TickCounter.ShiftTempoList(tempos, tickPrefix);

        TsslnTrackPitchData? pitchData = null;
        var parameters = stateInfo.Children("Parameter");
        foreach (var parameter in parameters)
        {
            var logF0Curves = parameter.Children("LogF0");
            if (logF0Curves.Count == 0)
                continue;
            var pitchEvents = ParseCurveData(logF0Curves);
            var vibAmpEvents = ParseCurveData(parameter.Children("VibAmp"));
            var vibFrqEvents = ParseCurveData(parameter.Children("VibFrq"));
            pitchData = new TsslnTrackPitchData(
                pitchEvents, tempos, tickPrefix, vibAmpEvents, vibFrqEvents);
        }

        timeSignatures = TickCounter.ShiftBeatList(timeSignatures, 1);

        string singerName = "";
        var voiceInfo = stateInfo.Children("VoiceInformation");
        if (voiceInfo.Count > 0)
            singerName = voiceInfo[0].GetString("CharacterName") ?? "";

        var singingTrack = new SingingTrack
        {
            Title = track.GetString("Name") ?? "",
            NoteList = notes,
            AiSingerName = singerName,
        };

        if (ImportPitch && pitchData != null)
        {
            var pitch = TsslnPitch.PitchFromTrack(pitchData);
            if (pitch != null)
                singingTrack.EditedParams.Pitch = pitch;
        }

        return (singingTrack, tempos, timeSignatures);
    }

    private static List<TsslnParamEvent> ParseCurveData(List<TsslnTree> curves)
    {
        var result = new List<TsslnParamEvent>();
        foreach (var curve in curves)
        {
            foreach (var dataNode in curve.Children("Data"))
            {
                double value = dataNode.GetDouble("Value") ?? 0;
                int? index = dataNode.GetInt("Index");
                if (index == 0)
                    index = null;
                int? repeat = dataNode.GetInt("Repeat");
                if (repeat == 0)
                    repeat = null;
                result.Add(new TsslnParamEvent(index, repeat, value));
            }
        }
        return result;
    }

    public override byte[] Dump(Project project)
    {
        _timeSynchronizer = new TimeSynchronizer(project.SongTempoList);
        _firstBarLength = (int)project.TimeSignatureList[0].BarLength();

        var defaultBeat = GenerateTimeSignatures(project.TimeSignatureList);
        var defaultTempo = GenerateTempos(project.SongTempoList);

        var tracksNode = new TsslnTreeBuilder("Tracks");
        int index = 1;
        foreach (var track in project.TrackList)
        {
            if (track is InstrumentalTrack instrumental)
            {
                var audioTrack = new TsslnTreeBuilder("Track");
                audioTrack.AddString("Name", $"Audio{index}");
                audioTrack.AddInt("State", 0);
                audioTrack.AddDouble("Volume", 0);
                audioTrack.AddDouble("Pan", 0);
                audioTrack.AddInt("Type", AudioTrackType);
                var audioEvent = new TsslnTreeBuilder("AudioEvent");
                audioEvent.AddString("Path", instrumental.AudioFilePath);
                audioEvent.AddDouble("Offset", _timeSynchronizer.GetActualSecsFromTicks(instrumental.Offset));
                audioTrack.AddChild(audioEvent.Node);
                tracksNode.AddChild(audioTrack.Node);
            }
            else if (track is SingingTrack singing)
            {
                var singingTrack = new TsslnTreeBuilder("Track");
                singingTrack.AddString("Name", $"Singer{index}");
                singingTrack.AddInt("State", 0);
                singingTrack.AddDouble("Volume", 0);
                singingTrack.AddDouble("Pan", 0);
                singingTrack.AddInt("Type", SingingTrackType);

                var stateInfo = new TsslnTreeBuilder("StateInformation");
                var song = new TsslnTreeBuilder("Song");
                song.AddChild(defaultTempo.Node);
                song.AddChild(defaultBeat.Node);
                var score = new TsslnTreeBuilder("Score");
                foreach (var noteNode in GenerateNotes(singing.NoteList))
                    score.AddChild(noteNode.Node);
                song.AddChild(score.Node);
                stateInfo.AddChild(song.Node);

                var logF0 = GeneratePitch(singing.EditedParams.Pitch, project.SongTempoList);
                if (logF0 != null)
                {
                    var parameter = new TsslnTreeBuilder("Parameter");
                    parameter.AddChild(logF0.Node);
                    stateInfo.AddChild(parameter.Node);
                }

                stateInfo.AddBool("TempoSync", false);
                stateInfo.AddString("VersionOfAppFileSaved", "1.8.0.17");

                singingTrack.AddBinary("PluginData", JuceBinary.Build(stateInfo.Node));
                tracksNode.AddChild(singingTrack.Node);
            }
            index++;
        }

        var root = new TsslnTreeBuilder("TSSolution");
        root.AddChild(tracksNode.Node);
        root.AddString("VersionOfAppFileSaved", "1.8.0.17");

        return JuceBinary.Build(root.Node);
    }

    private TsslnTreeBuilder GenerateTempos(List<SongTempo> tempos)
    {
        var tempoBuilder = new TsslnTreeBuilder("Tempo");
        for (int i = 0; i < tempos.Count; i++)
        {
            var sound = new TsslnTreeBuilder("Sound");
            sound.AddInt("Clock", i != 0 ? (int)Math.Round(tempos[i].Position * TsslnConstants.TickRate) : 0);
            sound.AddDouble("Tempo", tempos[i].Bpm);
            tempoBuilder.AddChild(sound.Node);
        }
        return tempoBuilder;
    }

    private TsslnTreeBuilder GenerateTimeSignatures(List<TimeSignature> timeSignatures)
    {
        var beat = new TsslnTreeBuilder("Beat");
        var firstTime = new TsslnTreeBuilder("Time");
        firstTime.AddInt("Clock", 0);
        firstTime.AddInt("Beats", timeSignatures[0].Numerator);
        firstTime.AddInt("BeatType", timeSignatures[0].Denominator);
        beat.AddChild(firstTime.Node);

        double tick = 0.0;
        var prev = timeSignatures[0];
        for (int i = 1; i < timeSignatures.Count; i++)
        {
            var ts = timeSignatures[i];
            if (ts.BarIndex > prev.BarIndex)
                tick += (ts.BarIndex - prev.BarIndex) * prev.BarLength();
            var time = new TsslnTreeBuilder("Time");
            time.AddInt("Clock", (int)(tick * TsslnConstants.TickRate));
            time.AddInt("Beats", ts.Numerator);
            time.AddInt("BeatType", ts.Denominator);
            beat.AddChild(time.Node);
            prev = ts;
        }
        return beat;
    }

    private static List<TsslnTreeBuilder> GenerateNotes(List<Note> notes)
    {
        var result = new List<TsslnTreeBuilder>();
        foreach (var note in notes)
        {
            var noteNode = new TsslnTreeBuilder("Note");
            noteNode.AddInt("Clock", (int)(note.StartPos * TsslnConstants.TickRate));
            noteNode.AddInt("Duration", (int)(note.Length * TsslnConstants.TickRate));
            noteNode.AddInt("PitchStep", note.KeyNumber % Constants.KeyInOctave);
            noteNode.AddInt("PitchOctave", note.KeyNumber / Constants.KeyInOctave + TsslnConstants.OctaveOffset);
            noteNode.AddString("Lyric", note.Lyric);
            noteNode.AddInt("Syllabic", 0);
            if (note.Pronunciation != null)
                noteNode.AddString("Phoneme", note.Pronunciation);
            result.Add(noteNode);
        }
        return result;
    }

    private TsslnTreeBuilder? GeneratePitch(ParamCurve pitch, List<SongTempo> tempoList)
    {
        var data = TsslnPitch.GenerateForTrack(pitch, tempoList, _firstBarLength);
        if (data == null)
            return null;
        var logF0 = new TsslnTreeBuilder("LogF0");
        logF0.AddInt("Length", data.Length);
        foreach (var ev in data.Events)
        {
            var dataNode = new TsslnTreeBuilder("Data");
            if (ev.Idx.HasValue)
                dataNode.AddInt("Index", ev.Idx.Value);
            if (ev.Repeat.HasValue)
                dataNode.AddInt("Repeat", ev.Repeat.Value);
            dataNode.AddDouble("Value", ev.Value);
            logF0.AddChild(dataNode.Node);
        }
        return logF0;
    }
}
