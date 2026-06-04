using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public sealed class SongTempo
{
    [JsonPropertyName("Position")] public int Position { get; set; } = 0;
    [JsonPropertyName("BPM")] public double Bpm { get; set; } = Constants.DefaultBpm;

    public SongTempo() { }
    public SongTempo(int position, double bpm) { Position = position; Bpm = bpm; }

    public SongTempo Clone() => new(Position, Bpm);
}

public sealed class TimeSignature
{
    [JsonPropertyName("BarIndex")] public int BarIndex { get; set; } = 0;
    [JsonPropertyName("Numerator")] public int Numerator { get; set; } = 4;
    [JsonPropertyName("Denominator")] public int Denominator { get; set; } = 4;

    public TimeSignature() { }
    public TimeSignature(int barIndex, int numerator, int denominator)
    {
        BarIndex = barIndex;
        Numerator = numerator;
        Denominator = denominator;
    }

    public double BarLength(int ticksInBeat = Constants.TicksInBeat) =>
        (double)(ticksInBeat * 4) * Numerator / Denominator;

    public TimeSignature Clone() => new(BarIndex, Numerator, Denominator);
}

public sealed class ParamCurve
{
    [JsonPropertyName("PointList")] public List<Point> Points { get; set; } = new();

    [JsonPropertyName("TotalPointsCount")] public int TotalPointsCount => Points.Count;

    public ParamCurve ReduceSampleRate(int interval, int interruptValue = 0)
    {
        if (interval <= 0)
            return this;
        var points = Points;
        if (points.Count <= 1)
            return this;
        var result = new List<Point>();
        int i = 0;
        while (i < points.Count)
        {
            var point = points[i];
            if (point.Y == interruptValue)
            {
                result.Add(point);
                i++;
                continue;
            }
            int runStart = i;
            while (i < points.Count && points[i].Y != interruptValue)
                i++;
            var run = points.GetRange(runStart, i - runStart);
            if (runStart > 0 && points[runStart - 1].Y == interruptValue)
            {
                result.Add(run[0]);
                run.RemoveAt(0);
            }
            if (run.Count == 0)
                continue;
            if (run.Count == 1)
            {
                result.Add(run[0]);
                continue;
            }
            var tailPoint = run[^1];
            var windowPoints = run.GetRange(0, run.Count - 1);
            int j = 0;
            while (j < windowPoints.Count)
            {
                int bucketStart = windowPoints[j].X;
                var bucket = new List<Point> { windowPoints[j] };
                j++;
                while (j < windowPoints.Count && windowPoints[j].X < bucketStart + interval)
                {
                    bucket.Add(windowPoints[j]);
                    j++;
                }
                result.Add(new Point(
                    (int)Math.Round(bucket.Average(p => (double)p.X)),
                    (int)Math.Round(bucket.Average(p => (double)p.Y))));
            }
            result.Add(tailPoint);
        }
        return new ParamCurve { Points = result };
    }

    public List<List<Point>> SplitIntoSegments(int interruptValue = 0)
    {
        int endPointX = Point.EndX;
        var segments = new List<List<Point>>();
        var points = Points;
        if (points.Count == 0)
            return segments;
        if (points.Count == 1)
        {
            var point = points[0];
            if (point.X >= 0 && point.X < endPointX && point.Y != interruptValue)
                segments.Add(new List<Point> { point });
            return segments;
        }
        var buffer = new List<Point>();
        if (interruptValue != 0)
        {
            foreach (var point in points)
            {
                if (point.X >= 0 && point.Y < endPointX)
                {
                    if (point.Y != interruptValue)
                        buffer.Add(point);
                    else if (buffer.Count > 0)
                    {
                        segments.Add(new List<Point>(buffer));
                        buffer.Clear();
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                var currentPoint = points[i];
                var nextPoint = points[i + 1];
                if (currentPoint.Y != interruptValue)
                    buffer.Add(currentPoint);
                else if (nextPoint.Y != interruptValue)
                {
                    if (currentPoint.X >= 0 && (i <= 0 || points[i - 1].Y != interruptValue))
                        buffer.Add(currentPoint);
                }
                else if (buffer.Count > 0)
                {
                    segments.Add(new List<Point>(buffer));
                    buffer.Clear();
                }
            }
            var lastPoint = points[^1];
            if (lastPoint.X < endPointX &&
                (lastPoint.Y != interruptValue || points[^2].Y != interruptValue))
                buffer.Add(lastPoint);
        }
        if (buffer.Count > 0)
            segments.Add(new List<Point>(buffer));
        return segments;
    }
}

public sealed class Params
{
    [JsonPropertyName("Pitch")] public ParamCurve Pitch { get; set; } = new();
    [JsonPropertyName("Volume")] public ParamCurve Volume { get; set; } = new();
    [JsonPropertyName("Breath")] public ParamCurve Breath { get; set; } = new();
    [JsonPropertyName("Gender")] public ParamCurve Gender { get; set; } = new();
    [JsonPropertyName("Strength")] public ParamCurve Strength { get; set; } = new();
}

public sealed class VibratoParam
{
    [JsonPropertyName("StartPercent")] public double StartPercent { get; set; } = 0.0;
    [JsonPropertyName("EndPercent")] public double EndPercent { get; set; } = 0.0;
    [JsonPropertyName("IsAntiPhase")] public bool IsAntiPhase { get; set; } = false;
    [JsonPropertyName("Amplitude")] public ParamCurve Amplitude { get; set; } = new();
    [JsonPropertyName("Frequency")] public ParamCurve Frequency { get; set; } = new();
}

public sealed class Phones
{
    [JsonPropertyName("HeadLengthInSecs")] public double HeadLengthInSecs { get; set; } = -1.0;
    [JsonPropertyName("MidRatioOverTail")] public double MidRatioOverTail { get; set; } = -1.0;
}

public sealed class Note
{
    [JsonPropertyName("StartPos")] public int StartPos { get; set; } = 0;
    [JsonPropertyName("Length")] public int Length { get; set; } = 0;
    [JsonPropertyName("KeyNumber")] public int KeyNumber { get; set; } = 0;
    [JsonPropertyName("HeadTag")] public string? HeadTag { get; set; }
    [JsonPropertyName("Lyric")] public string Lyric { get; set; } = "";
    [JsonPropertyName("Pronunciation")] public string? Pronunciation { get; set; }
    [JsonPropertyName("EditedPhones")] public Phones? EditedPhones { get; set; }
    [JsonPropertyName("Vibrato")] public VibratoParam? Vibrato { get; set; }

    [JsonIgnore] public int EndPos => StartPos + Length;
}

public abstract class Track
{
    [JsonPropertyName("Title")] public string Title { get; set; } = "";
    [JsonPropertyName("Mute")] public bool Mute { get; set; } = false;
    [JsonPropertyName("Solo")] public bool Solo { get; set; } = false;
    [JsonPropertyName("Volume")] public double Volume { get; set; } = 1.0;
    [JsonPropertyName("Pan")] public double Pan { get; set; } = 0.0;

    [JsonPropertyName("Type")] public abstract string Type { get; }
}

public sealed class SingingTrack : Track
{
    public override string Type => "Singing";

    [JsonPropertyName("AISingerName")] public string AiSingerName { get; set; } = "";
    [JsonPropertyName("ReverbPreset")] public string ReverbPreset { get; set; } = "";
    [JsonPropertyName("NoteList")] public List<Note> NoteList { get; set; } = new();
    [JsonPropertyName("EditedParams")] public Params EditedParams { get; set; } = new();
}

public sealed class InstrumentalTrack : Track
{
    public override string Type => "Instrumental";

    [JsonPropertyName("AudioFilePath")] public string AudioFilePath { get; set; } = "";
    [JsonPropertyName("Offset")] public int Offset { get; set; } = 0;
}

public sealed class Project
{
    [JsonPropertyName("Version")] public string Version { get; set; } = "";
    [JsonPropertyName("SongTempoList")] public List<SongTempo> SongTempoList { get; set; } = new();
    [JsonPropertyName("TimeSignatureList")] public List<TimeSignature> TimeSignatureList { get; set; } = new();
    [JsonPropertyName("TrackList")] public List<Track> TrackList { get; set; } = new();

    public static Project MergeProjects(IReadOnlyList<Project> projects)
    {
        if (projects.Count <= 1)
            throw new ArgumentException("No projects to merge");
        var sample = projects[0];
        var trackList = projects.SelectMany(p => p.TrackList).ToList();
        return new Project
        {
            Version = sample.Version,
            SongTempoList = sample.SongTempoList,
            TimeSignatureList = sample.TimeSignatureList,
            TrackList = trackList,
        };
    }

    public List<Project> SplitTracks(int maxTrackCount)
    {
        if (!TrackList.Any(t => t is SingingTrack))
            throw new InvalidOperationException("No singing tracks found");
        var singing = TrackList.OfType<SingingTrack>().ToList();
        var result = new List<Project>();
        for (int i = 0; i < singing.Count; i += maxTrackCount)
        {
            var chunk = singing.Skip(i).Take(maxTrackCount).Cast<Track>().ToList();
            result.Add(new Project
            {
                Version = Version,
                SongTempoList = SongTempoList,
                TimeSignatureList = TimeSignatureList,
                TrackList = chunk,
            });
        }
        return result;
    }
}
