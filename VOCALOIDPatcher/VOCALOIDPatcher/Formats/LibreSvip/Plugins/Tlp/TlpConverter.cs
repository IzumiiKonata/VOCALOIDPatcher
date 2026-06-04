using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tlp;

public sealed class TlpConverter : FormatConverter
{
    private static readonly Regex BareNaN = new(@"(?<=[\[,\s])NaN(?=[,\]\s])", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Options = new()
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
        string text = BareNaN.Replace(TextHelper.DetectAndDecode(content), "null");
        var tlp = JsonSerializer.Deserialize<TuneLabProject>(text, Options) ?? new TuneLabProject();
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
                        singingTrack.EditedParams.Pitch.Points.AddRange(ParsePitch(midi, (int)part.Pos, vbBase, vbEnv));
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
            foreach (var (value, posGroup) in IterTools.GroupByTransform(envelope.Values, p => p.Value, p => p.Pos))
            {
                double groupStart = _synchronizer.GetActualSecsFromTicks((int)posGroup[0]);
                double groupEnd = _synchronizer.GetActualSecsFromTicks((int)(posGroup[^1] + 5));
                envDict.SetConstant(groupStart, groupEnd, Math.Max(value + 1, 0));
            }
        }
        return (baseDict, envDict);
    }

    private List<Point> ParsePitch(TuneLabMidiPart part, int offset, PiecewiseIntervalDict vbBase, PiecewiseIntervalDict vbEnv)
    {
        const int firstBarLength = 1920;
        var points = new List<Point>();
        foreach (var pitchPart in part.Pitch)
        {
            var anchorGroup = IterTools
                .UniqueJustSeen(pitchPart.Where(p => !double.IsNaN(p.Value)), p => p.Pos)
                .ToList();
            if (anchorGroup.Count < 2)
                continue;
            var interpolator = new HermiteInterpolator(anchorGroup.Select(p => (p.Pos, p.Value)).ToList());
            var xs = IterTools.NumericRange(anchorGroup[0].Pos, anchorGroup[^1].Pos + 1, 5);
            var ys = interpolator.Interpolate(xs);
            for (int i = 0; i < xs.Count; i++)
            {
                int pitchPos = (int)xs[i] + offset;
                if (i == 0)
                    points.Add(new Point(pitchPos + firstBarLength, -100));
                double pitchSecs = _synchronizer.GetActualSecsFromTicks(pitchPos);
                double pitchValue = ys[i];
                double? vv = vbBase.Get(pitchSecs);
                if (vv != null)
                    pitchValue += vv.Value * vbEnv.Get(pitchSecs, 1);
                points.Add(new Point(pitchPos + firstBarLength, (int)Math.Round(pitchValue * 100)));
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
        var tlp = new TuneLabProject
        {
            Tempos = TickCounter.SkipTempoList(project.SongTempoList, _firstBarLength)
                .Select(t => new TuneLabTempo { Pos = t.Position, Bpm = t.Bpm }).ToList(),
            TimeSignatures = project.TimeSignatureList
                .Select(t => new TuneLabTimeSignature { Numerator = t.Numerator, Denominator = t.Denominator, BarIndex = t.BarIndex })
                .ToList(),
            Tracks = GenerateTracks(project.TrackList),
        };
        string json = JsonSerializer.Serialize(tlp, Options);
        return TextHelper.EncodeUtf8(json);
    }

    private List<TuneLabTrack> GenerateTracks(List<Track> trackList)
    {
        var result = new List<TuneLabTrack>();
        foreach (var track in trackList.OfType<SingingTrack>())
        {
            if (track.NoteList.Count == 0)
                continue;
            var midiPart = new TuneLabMidiPart
            {
                Name = track.Title,
                Pos = 0,
                Dur = Math.Ceiling((double)track.NoteList[^1].EndPos / _firstBarLength) * _firstBarLength,
                Notes = track.NoteList.Select(n => new TuneLabNote
                {
                    Pos = n.StartPos,
                    Dur = n.Length,
                    Pitch = n.KeyNumber,
                    Lyric = n.Lyric,
                    Pronunciation = n.Pronunciation,
                }).ToList(),
                Pitch = GeneratePitch(track.EditedParams.Pitch),
            };
            result.Add(new TuneLabTrack
            {
                Name = track.Title,
                Gain = GenerateVolume(track.Volume),
                Pan = track.Pan,
                Mute = track.Mute,
                Solo = track.Solo,
                Parts = new List<TuneLabPart> { midiPart },
            });
        }
        return result;
    }

    private static double GenerateVolume(double volume) =>
        volume > 0 ? Math.Max(MusicMath.RatioToDb(Math.Max(volume, 0.01)), -70) : -70;

    private List<List<TlpPoint>> GeneratePitch(ParamCurve pitch)
    {
        var result = new List<List<TlpPoint>>();
        foreach (var part in IterTools.SplitWhen(pitch.Points,
                     (a, b) => (a.Y == -100 && b.Y != -100) || (a.Y != -100 && b.Y == -100)))
        {
            if (part.Count == 0)
                continue;
            result.Add(part.Select(p => new TlpPoint(p.X - _firstBarLength, p.Y == -100 ? double.NaN : p.Y / 100.0)).ToList());
        }
        return result;
    }
}
