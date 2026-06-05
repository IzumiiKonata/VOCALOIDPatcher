using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal sealed class VsqGenerator
{
    private const string DefaultPhoneme = "4 a";
    private const int MaxBars = 4096;

    private readonly int _ticksPerBeat;
    private readonly Encoding _lyricEncoding;

    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });
    private int _firstBarLength;
    private List<TimeSignature> _timeSignatures = new();

    public VsqGenerator(int ticksPerBeat, Encoding lyricEncoding)
    {
        _ticksPerBeat = ticksPerBeat;
        _lyricEncoding = lyricEncoding;
    }

    private double TickRate => (double)Constants.TicksInBeat / _ticksPerBeat;

    public byte[] Generate(Project project)
    {
        project = LimitBars(project, MaxBars);
        _synchronizer = new TimeSynchronizer(project.SongTempoList.Count > 0
            ? project.SongTempoList : new List<SongTempo> { new() });
        _firstBarLength = (int)project.TimeSignatureList[0].BarLength(_ticksPerBeat);
        _timeSignatures = project.TimeSignatureList;

        var tracks = new List<List<MidiRawEvent>>();
        tracks.Add(BuildMasterTrack(project));
        tracks.AddRange(BuildSingingTracks(project.TrackList));

        var trackBytes = tracks.Select(events =>
            new MidiTrackBytes(SortStable(events))).ToList();
        return MidiWriter.Build(_ticksPerBeat, trackBytes);
    }

    private static List<MidiRawEvent> SortStable(List<MidiRawEvent> events)
    {
        return events.Select((e, i) => (e, i))
            .OrderBy(t => t.e.Tick).ThenBy(t => t.i)
            .Select(t => t.e).ToList();
    }

    private List<MidiRawEvent> BuildMasterTrack(Project project)
    {
        var events = new List<MidiRawEvent>
        {
            MakeTrackName(0, "Master Track"),
        };
        foreach (var tempo in project.SongTempoList)
        {
            int tick = (int)Math.Round(tempo.Position / TickRate);
            events.Add(MidiTrackBytes.MakeTempoEvent(tick, MidiFile.Bpm2Tempo(tempo.Bpm)));
        }
        AddTimeSignatures(events, project.TimeSignatureList);
        return events;
    }

    private void AddTimeSignatures(List<MidiRawEvent> events, List<TimeSignature> timeSignatures)
    {
        int ticks = 0;
        TimeSignature? prev = null;
        foreach (var ts in timeSignatures)
        {
            if (prev != null)
                ticks += (int)Math.Round(prev.BarLength(_ticksPerBeat) * (ts.BarIndex - prev.BarIndex));
            events.Add(MidiTrackBytes.MakeTimeSigEvent(ticks, ts.Numerator, ts.Denominator));
            prev = ts;
        }
    }

    private List<List<MidiRawEvent>> BuildSingingTracks(List<Track> tracks)
    {
        var result = new List<List<MidiRawEvent>>();
        var singingTracks = tracks.OfType<SingingTrack>().Where(t => t.NoteList.Count > 0).ToList();
        for (int i = 0; i < singingTracks.Count; i++)
        {
            string text = GenerateTrackText(singingTracks[i], i, singingTracks.Count);
            result.Add(BuildTextEvents(text));
        }
        return result;
    }

    private List<MidiRawEvent> BuildTextEvents(string trackText)
    {
        var events = new List<MidiRawEvent>();
        string remaining = trackText;
        while (remaining.Length != 0)
        {
            int eventId = events.Count;
            string idStr = eventId.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            string header = $"DM:{idStr}:";
            int availableLength = 0x7F - header.Length;
            if (availableLength <= 0)
                availableLength = 1;
            int take = Math.Min(availableLength, remaining.Length);
            string chunk = header + remaining.Substring(0, take);
            events.Add(MidiTrackBytes.MakeTextEvent(0, EncodeText(chunk)));
            remaining = remaining.Substring(take);
        }
        return events;
    }

    private string GenerateTrackText(SingingTrack track, int trackIndex, int tracksCount)
    {
        var notesLines = new List<string>();
        var lyricsLines = new List<string>();
        int tickPrefix = _firstBarLength;
        var tickList = track.NoteList.Select(n => n.StartPos + tickPrefix).ToList();

        for (int i = 0; i < track.NoteList.Count; i++)
        {
            var note = track.NoteList[i];
            string number = (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            notesLines.AddRange(new[]
            {
                $"[ID#{number}]",
                "Type=Anote",
                $"Length={note.Length}",
                $"Note#={note.KeyNumber}",
                "Dynamics=64",
                "PMBendDepth=0",
                "PMBendLength=0",
                "PMbPortamentoUse=0",
                "DEMdecGainRate=0",
                "DEMaccent=0",
                $"LyricHandle=h#{number}",
            });
            string lyric = note.Lyric;
            string xsampa = VsqxPhonemeMaps.LegatoChars.Contains(lyric)
                ? "-"
                : VsqxPhonemeMaps.Romaji2Xsampa.TryGetValue(lyric, out var x) ? x : DefaultPhoneme;
            lyricsLines.Add($"[h#{number}]");
            lyricsLines.Add($"L0=\"{lyric}\",\"{xsampa}\",0.000000,64,0,0");
        }

        var result = new List<string>
        {
            "[Common]",
            "Version=DSB301",
            $"Name={track.Title}",
            "Color=181,162,123",
            "DynamicsMode=1",
            "PlayMode=1",
        };
        if (trackIndex == 0)
        {
            result.AddRange(new[]
            {
                "[Master]",
                "PreMeasure=1",
                "[Mixer]",
                "MasterFeder=0",
                "MasterPanpot=0",
                "MasterMute=0",
                "OutputMode=0",
                $"Tracks={tracksCount}",
            });
            for (int i = 0; i < tracksCount; i++)
                result.AddRange(new[] { $"Feder{i}=0", $"Panpot{i}=0", $"Mute{i}=0", $"Solo{i}=0" });
        }
        result.Add("[EventList]");
        result.Add("0=ID#0000");
        for (int i = 0; i < tickList.Count; i++)
        {
            string idx = (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            result.Add($"{tickList[i]}=ID#{idx}");
        }
        result.Add($"{track.NoteList[^1].EndPos + tickPrefix}=EOS");
        result.Add("[ID#0000]");
        result.Add("Type=Singer");
        result.Add("IconHandle=h#0000");
        result.AddRange(notesLines);
        result.AddRange(new[]
        {
            "[h#0000]",
            "IconID=$07010000",
            $"IDS={track.AiSingerName}",
            "Original=0",
            "Caption=",
            "Length=1",
            "Language=0",
            "Program=0",
        });
        result.AddRange(lyricsLines);
        result.AddRange(GeneratePitchText(track.EditedParams.Pitch, tickPrefix, track.NoteList));
        result.AddRange(GenerateParamsText(track.EditedParams, tickPrefix));
        return string.Join("\n", result);
    }

    private List<string> GeneratePitchText(ParamCurve pitch, int tickPrefix, List<Note> noteList)
    {
        var result = new List<string>();
        var handler = new VocaloidPitchHandler(_synchronizer, noteList, _timeSignatures, _firstBarLength);
        var pitchData = handler.FromAbsolutePitch(pitch);
        if (pitchData.IsEmpty)
            return result;
        if (pitchData.Pit.Events.Count > 0)
        {
            result.Add("[PitchBendBPList]");
            foreach (var ev in pitchData.Pit.Events)
                result.Add($"{ev.Pos + tickPrefix}={ev.Value}");
        }
        if (pitchData.Pbs.Events.Count > 0)
        {
            result.Add("[PitchBendSensBPList]");
            foreach (var ev in pitchData.Pbs.Events)
                result.Add($"{ev.Pos + tickPrefix}={ev.Value}");
        }
        return result;
    }

    private List<string> GenerateParamsText(Params parameters, int tickPrefix)
    {
        var result = new List<string>();
        AddCurveSection(result, parameters.Volume.Points, "dynamics", "DynamicsBPList", tickPrefix, false);
        AddCurveSection(result, parameters.Breath.Points, "breathiness", "BreathinessBPList", tickPrefix, false);
        AddCurveSection(result, parameters.Gender.Points, "gender", "GenderFactorBPList", tickPrefix, true);
        AddCurveSection(result, parameters.Strength.Points, "brightness", "BrightnessBPList", tickPrefix, false);
        return result;
    }

    private void AddCurveSection(List<string> result, List<Point> points, string paramName,
        string sectionName, int tickPrefix, bool reverse)
    {
        var curve = VsqControllerHandler.ConvertParamPointsToVocaloidCurve(
            points, paramName, -_firstBarLength, reverse);
        if (curve.IsEmpty)
            return;
        result.Add($"[{sectionName}]");
        foreach (var ev in curve.Events)
            result.Add($"{ev.Pos + tickPrefix}={ev.Value}");
    }

    private MidiRawEvent MakeTrackName(int tick, string name)
    {
        byte[] nameBytes = EncodeText(name);
        var data = new byte[2 + 1 + nameBytes.Length];
        data[0] = 0xFF;
        data[1] = 0x03;
        data[2] = (byte)nameBytes.Length;
        Array.Copy(nameBytes, 0, data, 3, nameBytes.Length);
        return new MidiRawEvent(tick, data);
    }

    private byte[] EncodeText(string text)
    {
        var encoder = (Encoding)_lyricEncoding.Clone();
        encoder.EncoderFallback = new EncoderReplacementFallback("?");
        return encoder.GetBytes(text);
    }

    private static Project LimitBars(Project project, int maxBars)
    {
        var timeSignatures = project.TimeSignatureList.Where(ts => ts.BarIndex < maxBars).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        int firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());
        int maxTicks = maxBars * firstBarLength;

        var newTracks = new List<Track>();
        foreach (var track in project.TrackList)
        {
            if (track is SingingTrack singing)
            {
                var clone = new SingingTrack
                {
                    Title = singing.Title,
                    Mute = singing.Mute,
                    Solo = singing.Solo,
                    Volume = singing.Volume,
                    Pan = singing.Pan,
                    AiSingerName = singing.AiSingerName,
                    ReverbPreset = singing.ReverbPreset,
                    NoteList = singing.NoteList.Where(n => n.EndPos < maxTicks).ToList(),
                };
                clone.EditedParams.Pitch.Points = LimitPoints(singing.EditedParams.Pitch.Points, firstBarLength, maxTicks);
                clone.EditedParams.Volume.Points = LimitPoints(singing.EditedParams.Volume.Points, firstBarLength, maxTicks);
                clone.EditedParams.Breath.Points = LimitPoints(singing.EditedParams.Breath.Points, firstBarLength, maxTicks);
                clone.EditedParams.Gender.Points = LimitPoints(singing.EditedParams.Gender.Points, firstBarLength, maxTicks);
                clone.EditedParams.Strength.Points = LimitPoints(singing.EditedParams.Strength.Points, firstBarLength, maxTicks);
                newTracks.Add(clone);
            }
            else
            {
                newTracks.Add(track);
            }
        }

        return new Project
        {
            Version = project.Version,
            SongTempoList = project.SongTempoList.Where(t => t.Position < maxTicks).ToList(),
            TimeSignatureList = timeSignatures,
            TrackList = newTracks,
        };
    }

    private static List<Point> LimitPoints(List<Point> points, int firstBarTicks, int maxTicks)
    {
        var result = new List<Point>();
        foreach (var p in points)
            if (p.X - firstBarTicks < maxTicks)
                result.Add(p);
        return result;
    }
}
