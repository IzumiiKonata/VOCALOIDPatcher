using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlpx;

public sealed class TlpxConverter : FormatConverter
{
    private static readonly JsonSerializerOptions JOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumental { get; set; } = true;

    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var root = TlpxCbor.Decode(content);
        string json = CborToJson(root);
        var tlp = JsonSerializer.Deserialize<TuneLabProject>(json, JOpts) ?? new TuneLabProject();

        var timeSignatures = tlp.TimeSignatures
            .Select(t => new TimeSignature(t.BarIndex, t.Numerator, t.Denominator)).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        _firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());

        var tempos = TickCounter.ShiftTempoList(
            tlp.Tempos.Select(t => new SongTempo((int)t.Pos, t.Bpm)).ToList(), _firstBarLength);
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());
        _synchronizer = new TimeSynchronizer(tempos);

        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = timeSignatures,
            TrackList = ParseTracks(tlp.Tracks),
        };
    }

    private List<Track> ParseTracks(List<TuneLabTrack> tracks)
    {
        var trackList = new List<Track>();
        foreach (var track in tracks)
        {
            foreach (var part in track.Parts)
            {
                if (part is TuneLabAudioPart audio && ImportInstrumental)
                {
                    trackList.Add(new InstrumentalTrack
                    {
                        AudioFilePath = audio.Path,
                        Title = audio.Name,
                        Offset = (int)audio.Pos,
                        Volume = ParseVolume(track.Gain),
                        Pan = track.Pan,
                        Mute = track.Mute,
                        Solo = track.Solo,
                    });
                }
                else if (part is TuneLabMidiPart midi && midi.Notes.Count > 0)
                {
                    SingingTrack singingTrack;
                    if (trackList.Count > 0 && trackList[^1] is SingingTrack last
                        && (last.NoteList.Count == 0 || last.NoteList[^1].EndPos <= (int)part.Pos))
                    {
                        singingTrack = last;
                    }
                    else
                    {
                        singingTrack = new SingingTrack
                        {
                            Title = part.Name,
                            Volume = ParseVolume(track.Gain),
                            Pan = track.Pan,
                            Mute = track.Mute,
                            Solo = track.Solo,
                        };
                        trackList.Add(singingTrack);
                    }
                    singingTrack.NoteList.AddRange(ParseNotes(midi.Notes, (int)part.Pos));
                    if (ImportPitch)
                    {
                        var (vbBase, vbEnv) = ParseVibrato(midi);
                        singingTrack.EditedParams.Pitch.Points.AddRange(
                            ParsePitch(midi, (int)part.Pos, vbBase, vbEnv));
                    }
                }
            }
        }
        return trackList;
    }

    private static double ParseVolume(double gain) =>
        gain >= 0 ? Math.Min(gain / MusicMath.RatioToDb(4) + 1.0, 2.0) : MusicMath.DbToFloat(gain);

    private static List<Note> ParseNotes(List<TuneLabNote> notes, int offset)
    {
        var noteList = new List<Note>();
        int? nextPos = null;
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            var tlpNote = notes[i];
            int normalizedDuration = (int)tlpNote.Dur;
            if (nextPos != null)
            {
                double distance = nextPos.Value - tlpNote.Pos;
                if (distance < normalizedDuration)
                    normalizedDuration = (int)distance;
            }
            if (normalizedDuration > 0)
                noteList.Insert(0, new Note
                {
                    StartPos = (int)(tlpNote.Pos + offset),
                    Length = normalizedDuration,
                    KeyNumber = tlpNote.Pitch,
                    Lyric = tlpNote.Lyric,
                    Pronunciation = tlpNote.Pronunciation,
                });
            nextPos = (int)tlpNote.Pos;
        }
        return noteList;
    }

    private (PiecewiseIntervalDict, PiecewiseIntervalDict) ParseVibrato(TuneLabMidiPart part)
    {
        var baseDict = new PiecewiseIntervalDict();
        var envDict = new PiecewiseIntervalDict();
        foreach (var vibrato in part.Vibratos)
        {
            double vStart = _synchronizer.GetActualSecsFromTicks((int)vibrato.Pos);
            double vEnd = _synchronizer.GetActualSecsFromTicks((int)(vibrato.Pos + vibrato.Dur));
            var v = vibrato;
            double start = vStart;
            baseDict.Set(vStart, vEnd, secs =>
                Math.Sin(Math.PI * (2 * (secs - start) * v.Frequency - v.Phase)) * v.Amplitude);
        }
        if (part.Automations.TryGetValue("VibratoEnvelope", out var envelope))
        {
            foreach (var (value, posGroup) in IterTools.GroupByTransform(
                envelope.Values, p => p.Value, p => p.Pos))
            {
                double groupStart = _synchronizer.GetActualSecsFromTicks((int)posGroup[0]);
                double groupEnd = _synchronizer.GetActualSecsFromTicks((int)(posGroup[^1] + 5));
                envDict.SetConstant(groupStart, groupEnd, Math.Max(value + 1, 0));
            }
        }
        return (baseDict, envDict);
    }

    private List<Point> ParsePitch(TuneLabMidiPart part, int offset,
        PiecewiseIntervalDict vbBase, PiecewiseIntervalDict vbEnv)
    {
        var points = new List<Point>();
        foreach (var pitchPart in part.Pitch)
        {
            var anchorGroup = IterTools
                .UniqueJustSeen(pitchPart.Where(p => !double.IsNaN(p.Value)), p => p.Pos)
                .ToList();
            if (anchorGroup.Count < 2)
                continue;
            var interpolator = new HermiteInterpolator(
                anchorGroup.Select(p => (p.Pos, p.Value)).ToList());
            var xs = IterTools.NumericRange(anchorGroup[0].Pos, anchorGroup[^1].Pos + 1, 5);
            var ys = interpolator.Interpolate(xs);
            for (int i = 0; i < xs.Count; i++)
            {
                int pitchPos = (int)xs[i] + offset;
                if (i == 0)
                    points.Add(new Point(pitchPos + _firstBarLength, -100));
                double pitchSecs = _synchronizer.GetActualSecsFromTicks(pitchPos);
                double pitchValue = ys[i];
                double? vv = vbBase.Get(pitchSecs);
                if (vv != null)
                    pitchValue += vv.Value * vbEnv.Get(pitchSecs, 1);
                points.Add(new Point(pitchPos + _firstBarLength, (int)Math.Round(pitchValue * 100)));
            }
            if (points.Count > 0)
                points.Add(new Point(points[^1].X, -100));
        }
        return points;
    }

    public override byte[] Dump(Project project)
    {
        _synchronizer = new TimeSynchronizer(project.SongTempoList);
        _firstBarLength = (int)Math.Round(project.TimeSignatureList[0].BarLength());

        var cborTempos = TickCounter.SkipTempoList(project.SongTempoList, _firstBarLength)
            .Select(t => (object?)MakeDict(("pos", (object?)(double)t.Position), ("bpm", t.Bpm)))
            .ToList();

        var cborTimeSigs = project.TimeSignatureList
            .Select(t => (object?)MakeDict(
                ("numerator", (object?)(long)t.Numerator),
                ("denominator", (long)t.Denominator),
                ("barIndex", (long)t.BarIndex)))
            .ToList();

        var cborTracks = DumpTracks(project.TrackList);

        var root = MakeDict(
            ("version", (object?)(long)0),
            ("tempos", (object?)cborTempos),
            ("timeSignatures", (object?)cborTimeSigs),
            ("tracks", (object?)cborTracks));

        return TlpxCbor.Encode(root);
    }

    private List<object?> DumpTracks(List<Track> trackList)
    {
        var result = new List<object?>();
        foreach (var track in trackList.OfType<SingingTrack>())
        {
            if (track.NoteList.Count == 0)
                continue;

            var pitchGroups = DumpPitch(track.EditedParams.Pitch);

            double dur = Math.Ceiling((double)track.NoteList[^1].EndPos / _firstBarLength)
                * _firstBarLength;

            var notes = track.NoteList.Select(n =>
            {
                var d = MakeDict(
                    ("pos", (object?)(double)n.StartPos),
                    ("dur", (double)n.Length),
                    ("pitch", (long)n.KeyNumber),
                    ("lyric", (object?)n.Lyric));
                if (n.Pronunciation != null)
                    d["pronunciation"] = (object?)n.Pronunciation;
                return (object?)d;
            }).ToList();

            var midiPart = MakeDict(
                ("type", (object?)"midi"),
                ("name", (object?)track.Title),
                ("pos", (object?)0.0),
                ("dur", (object?)dur),
                ("gain", (object?)0.0),
                ("notes", (object?)notes),
                ("pitch", (object?)(List<object?>)pitchGroups.Select(g => (object?)g).ToList()),
                ("vibratos", (object?)new List<object?>()),
                ("automations", (object?)new Dictionary<string, object?>()));

            var tlpTrack = MakeDict(
                ("name", (object?)track.Title),
                ("gain", (object?)GenerateVolume(track.Volume)),
                ("pan", (object?)track.Pan),
                ("mute", (object?)track.Mute),
                ("solo", (object?)track.Solo),
                ("color", (object?)""),
                ("parts", (object?)new List<object?> { midiPart }));

            result.Add(tlpTrack);
        }
        return result;
    }

    private static double GenerateVolume(double volume) =>
        volume > 0 ? Math.Max(MusicMath.RatioToDb(Math.Max(volume, 0.01)), -70) : -70;

    private List<List<object?>> DumpPitch(ParamCurve pitch)
    {
        var result = new List<List<object?>>();
        foreach (var part in IterTools.SplitWhen(pitch.Points,
                     (a, b) => (a.Y == -100 && b.Y != -100) || (a.Y != -100 && b.Y == -100)))
        {
            if (part.Count == 0)
                continue;
            var flat = new List<object?>();
            foreach (var p in part)
            {
                flat.Add((object?)(double)(p.X - _firstBarLength));
                flat.Add(p.Y == -100 ? (object?)double.NaN : p.Y / 100.0);
            }
            result.Add(flat);
        }
        return result;
    }

    private static Dictionary<string, object?> MakeDict(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in pairs)
            d[k] = v;
        return d;
    }

    private static string CborToJson(object? obj)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms,
            new JsonWriterOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        WriteCborAsJson(writer, obj);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCborAsJson(Utf8JsonWriter writer, object? obj)
    {
        switch (obj)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case double d when double.IsNaN(d):
                writer.WriteNullValue();
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f when float.IsNaN(f):
                writer.WriteNullValue();
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case List<object?> list:
                writer.WriteStartArray();
                foreach (var item in list)
                    WriteCborAsJson(writer, item);
                writer.WriteEndArray();
                break;
            case Dictionary<string, object?> dict:
                writer.WriteStartObject();
                foreach (var kv in dict)
                {
                    writer.WritePropertyName(kv.Key);
                    WriteCborAsJson(writer, kv.Value);
                }
                writer.WriteEndObject();
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
