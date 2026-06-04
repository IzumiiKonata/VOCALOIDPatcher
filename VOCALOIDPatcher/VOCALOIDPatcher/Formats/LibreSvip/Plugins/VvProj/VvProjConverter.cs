using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.VvProj;

public sealed class VvProjConverter : FormatConverter
{
    private const double SecsStep = 4.0 / 375;

    public bool ImportPitch { get; set; } = true;

    private int _firstBarLength;
    private TimeSynchronizer _synchronizer = new(new List<SongTempo> { new() });

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var vv = JsonHelper.Deserialize<VoiceVoxProject>(TextHelper.DetectAndDecode(content));
        var timeSignatures = vv.Song.TimeSignatures
            .Select(t => new TimeSignature(t.MeasureNumber - 1, t.Beats, t.BeatType)).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());
        _firstBarLength = (int)timeSignatures[0].BarLength(vv.Song.Tpqn);
        var tempos = vv.Song.Tempos.Select(t => new SongTempo(t.Position, t.Bpm)).ToList();
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());
        _synchronizer = new TimeSynchronizer(tempos);

        var trackList = new List<Track>();
        foreach (var key in vv.Song.TrackOrder)
        {
            if (vv.Song.Tracks.TryGetValue(key, out var track))
                trackList.Add(ParseTrack(track));
        }
        return new Project
        {
            TimeSignatureList = timeSignatures,
            SongTempoList = tempos,
            TrackList = trackList,
        };
    }

    private SingingTrack ParseTrack(VoiceVoxTrack track)
    {
        var singing = new SingingTrack
        {
            Title = track.Name,
            Solo = track.Solo,
            Mute = track.Mute,
            Volume = track.Gain,
            Pan = track.Pan,
            NoteList = track.Notes.Select(n => new Note
            {
                StartPos = n.Position,
                Length = n.Duration,
                Lyric = n.Lyric,
                KeyNumber = n.NoteNumber,
            }).ToList(),
        };
        if (ImportPitch)
            singing.EditedParams.Pitch = new ParamCurve { Points = ParsePitch(track.PitchEditData) };
        return singing;
    }

    private List<Point> ParsePitch(List<double> pitchEditData)
    {
        double secs = 0;
        var points = new List<Point> { Point.StartPoint() };
        foreach (var part in IterTools.SplitWhen(pitchEditData, (x, y) => (x == -1 ? 1 : 0) + (y == -1 ? 1 : 0) == 1))
        {
            if (part.All(v => v == -1))
            {
                secs += SecsStep * part.Count;
            }
            else
            {
                points.Add(new Point((int)_synchronizer.GetActualTicksFromSecs(secs) + _firstBarLength, -100));
                foreach (var value in part)
                {
                    points.Add(new Point((int)_synchronizer.GetActualTicksFromSecs(secs) + _firstBarLength,
                        (int)Math.Round(MusicMath.Hz2Midi(value) * 100)));
                    secs += SecsStep;
                }
                points.Add(new Point((int)_synchronizer.GetActualTicksFromSecs(secs) + _firstBarLength, -100));
            }
        }
        points.Add(Point.EndPoint());
        return points;
    }

    public override byte[] Dump(Project project)
    {
        _synchronizer = new TimeSynchronizer(project.SongTempoList);
        _firstBarLength = (int)project.TimeSignatureList[0].BarLength();
        var song = new VoiceVoxSong
        {
            Tempos = project.SongTempoList.Select(t => new VoiceVoxTempo { Bpm = (int)Math.Round(t.Bpm), Position = t.Position }).ToList(),
            TimeSignatures = project.TimeSignatureList.Select(t => new VoiceVoxTimeSignature
            {
                MeasureNumber = t.BarIndex + 1,
                Beats = t.Numerator,
                BeatType = t.Denominator,
            }).ToList(),
        };
        foreach (var track in project.TrackList.OfType<SingingTrack>())
        {
            string key = Guid.NewGuid().ToString();
            song.TrackOrder.Add(key);
            song.Tracks[key] = new VoiceVoxTrack
            {
                Name = track.Title,
                Solo = track.Solo,
                Mute = track.Mute,
                Gain = track.Volume,
                Notes = track.NoteList.Select(n => new VoiceVoxNote
                {
                    Lyric = n.Lyric,
                    Position = n.StartPos,
                    Duration = n.Length,
                    NoteNumber = n.KeyNumber,
                }).ToList(),
                PitchEditData = GeneratePitch(track.EditedParams.Pitch),
            };
        }
        return TextHelper.EncodeUtf8(JsonHelper.Serialize(new VoiceVoxProject { Song = song }));
    }

    private List<double> GeneratePitch(ParamCurve pitch)
    {
        var dict = new PiecewiseIntervalDict();
        double maxPitchSecs = 0;
        double? prevSecs = null;
        double prevFreq = -1;
        foreach (var point in pitch.Points)
        {
            if (point.Y == -100)
            {
                if (prevSecs != null)
                    maxPitchSecs = prevSecs.Value;
                prevSecs = null;
                prevFreq = -1;
            }
            else
            {
                double secs = _synchronizer.GetActualSecsFromTicks(point.X - _firstBarLength);
                double freq = MusicMath.Midi2Hz(point.Y / 100.0);
                if (prevSecs != null)
                {
                    double ps = prevSecs.Value, pf = prevFreq, s = secs, f = freq;
                    dict.Set(ps, s, x => MusicMath.LinearInterpolation(x, (ps, pf), (s, f)));
                }
                else
                {
                    double f = freq;
                    dict.Set(secs, secs + 1e-6, _ => f);
                }
                prevSecs = secs;
                prevFreq = freq;
            }
        }
        var result = new List<double>();
        foreach (var secs in IterTools.NumericRange(0, maxPitchSecs, SecsStep))
            result.Add(dict.Get(secs, -1));
        return result;
    }
}
