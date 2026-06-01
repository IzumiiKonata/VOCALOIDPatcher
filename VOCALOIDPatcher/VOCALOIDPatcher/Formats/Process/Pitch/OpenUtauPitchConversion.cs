using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record OpenUtauNotePitchData(
    IReadOnlyList<OpenUtauNotePitchData.Point> Points,
    UtauNoteVibratoParams Vibrato)
{
    public sealed record Point(double X, double Y, Shape Shape);

    public enum Shape
    {
        EaseIn,
        EaseOut,
        EaseInOut,
        Linear,
    }

    public static string ShapeToText(Shape shape) => shape switch
    {
        Shape.EaseIn => "i",
        Shape.EaseOut => "o",
        Shape.EaseInOut => "io",
        Shape.Linear => "l",
        _ => "io",
    };

    public static Shape ShapeFromText(string? text) => text switch
    {
        "i" => Shape.EaseIn,
        "o" => Shape.EaseOut,
        "io" => Shape.EaseInOut,
        "l" => Shape.Linear,
        _ => Shape.EaseInOut,
    };
}

public sealed record OpenUtauPartPitchData(
    IReadOnlyList<OpenUtauPartPitchData.Point> Points,
    IReadOnlyList<OpenUtauNotePitchData> Notes)
{
    public sealed record Point(long X, int Y);
}

public static class OpenUtauPitchConversion
{
    private const long SamplingIntervalTick = 5L;
    private const long SafeSamplingIntervalTick = 5L;

    public static Model.Pitch? PitchFromUstxPart(IReadOnlyList<Note> notes, OpenUtauPartPitchData pitchData, IReadOnlyList<Tempo> tempos)
    {
        var notePointsList = new List<List<(long Tick, double Value)>>();
        var transformer = new TickTimeTransformer(tempos);
        long lastKeyPos = -SafeSamplingIntervalTick;

        int count = Math.Min(notes.Count, pitchData.Notes.Count);
        for (int i = 0; i < count; i++)
        {
            var note = notes[i];
            var notePitch = pitchData.Notes[i];
            var points = new List<(long Tick, double Value)>();
            var lastPointShape = OpenUtauNotePitchData.Shape.EaseInOut;
            double noteStartInMillis = transformer.TickToMilliSec(note.TickOn);
            var keyPointPositions = new List<long>();

            foreach (var rawPoint in notePitch.Points)
            {
                long x = Math.Max(transformer.MilliSecToTick(noteStartInMillis + rawPoint.X), lastKeyPos + SafeSamplingIntervalTick);
                lastKeyPos = x;
                keyPointPositions.Add(x);
                double y = rawPoint.Y / 10;
                var thisPoint = (x, y);
                if (points.Count > 0 && y != points[^1].Value)
                {
                    var interpolated = Interpolate(points[^1], thisPoint, lastPointShape);
                    points.AddRange(interpolated.Skip(1));
                }
                else
                {
                    points.Add(thisPoint);
                }

                lastPointShape = rawPoint.Shape;
            }

            AppendStartAndEndPoint(points, note);
            var pointsBefore = points.Where(p => p.Tick < note.TickOn).ToList();
            var pointsNotBefore = points.Where(p => p.Tick >= note.TickOn).ToList();
            var pointsAfter = pointsNotBefore.Where(p => p.Tick > note.TickOff).ToList();
            var pointsIn = pointsNotBefore.Where(p => p.Tick <= note.TickOff).ToList();
            var pointsInNoteWithVibrato = pointsIn.AppendUtauNoteVibrato(notePitch.Vibrato, note, transformer, SamplingIntervalTick);
            var pointsWithVibrato = pointsBefore.Concat(pointsInNoteWithVibrato).Concat(pointsAfter).ToList();
            var pointsResampled = Resampled(pointsWithVibrato, SamplingIntervalTick, keyPointPositions);
            notePointsList.Add(pointsResampled);
        }

        var currentSection = new List<(Note Note, List<(long Tick, double Value)> Points)>();
        var notePitchSections = new List<List<(Note Note, List<(long Tick, double Value)> Points)>> { currentSection };
        for (int i = 0; i < notePointsList.Count; i++)
        {
            var noteWithPoints = (notes[i], notePointsList[i]);
            if (currentSection.Count == 0)
            {
                currentSection.Add(noteWithPoints);
                continue;
            }

            var lastNote = currentSection[^1].Note;
            if (lastNote.TickOff < noteWithPoints.Item1.TickOn)
            {
                currentSection = new List<(Note, List<(long, double)>)> { noteWithPoints };
                notePitchSections.Add(currentSection);
            }
            else
            {
                currentSection.Add(noteWithPoints);
            }
        }

        long sectionBorder = 0L;
        var allPointsFromNote = new List<(long Tick, double Value)>();
        foreach (var section in notePitchSections)
        {
            if (section.Count == 0)
                continue;

            Note? lastNote = null;
            var pointsByNote = new List<List<(long Tick, double Value)>>();
            foreach (var (note, pts) in section)
            {
                var prevNote = lastNote;
                var adjusted = pts.Select(p =>
                {
                    int baseY = prevNote != null && p.Tick < note.TickOn ? prevNote.Key - note.Key : 0;
                    return (p.Tick, p.Value - baseY);
                }).ToList();
                pointsByNote.Add(adjusted);
                lastNote = note;
            }

            long nextSectionBorder = section[^1].Note.TickOff;
            var merged = pointsByNote
                .SelectMany(x => x)
                .GroupBy(p => p.Tick)
                .Where(g => g.Key >= sectionBorder && g.Key <= nextSectionBorder)
                .Select(g => (g.Key, g.Sum(p => p.Value)));
            allPointsFromNote.AddRange(merged);
            sectionBorder = nextSectionBorder;
        }

        var curvePoints = Resampled(pitchData.Points.Select(p => (p.X, p.Y / 100.0)).ToList(), SamplingIntervalTick);

        var pitchPoints = allPointsFromNote.Concat(curvePoints)
            .GroupBy(p => p.Tick)
            .Select(g => (Tick: g.Key, Value: g.Sum(p => p.Value)))
            .OrderBy(p => p.Tick)
            .Where(p => p.Tick >= 0)
            .Select(p => ((long, double?))(p.Tick, p.Value))
            .ToList();

        return pitchPoints.Count == 0 ? null : new Model.Pitch(pitchPoints, false);
    }

    public static Model.Pitch? MergePitchFromUstxParts(Model.Pitch? first, Model.Pitch? second)
    {
        if (first == null)
            return second;
        if (second == null)
            return first;

        var data = first.Data.Concat(second.Data)
            .Where(p => p.Value.HasValue)
            .Select(p => (p.Tick, p.Value!.Value))
            .GroupBy(p => p.Tick)
            .Select(g => ((long, double?))(g.Key, g.Sum(p => p.Item2)))
            .OrderBy(p => p.Item1)
            .ToList();
        return first with { Data = data };
    }

    public static Model.Pitch? ReduceRepeatedPitchPointsFromUstxTrack(this Model.Pitch? pitch)
    {
        if (pitch == null)
            return null;
        var data = pitch.Data.Select(p => (p.Tick, p.Value!.Value)).ToList().ReduceRepeatedPitchPoints()
            .Select(p => ((long, double?))(p.Tick, p.Value)).ToList();
        return pitch with { Data = data };
    }

    public static List<(long Tick, double Value)> ToOpenUtauPitchData(this Model.Pitch? pitch, IReadOnlyList<Note> notes)
    {
        var data = pitch?.GetRelativeData(notes);
        if (data == null)
            return new List<(long, double)>();
        var mapped = data.Select(p => (p.Tick, (double)(long)Math.Round(p.Value * 100))).ToList();
        return AppendPitchPointsForOpenUtauOutput(mapped).ReduceRepeatedPitchPoints();
    }

    private static List<(long Tick, double Value)> Interpolate(
        (long Tick, double Value) lastPoint,
        (long Tick, double Value) thisPoint,
        OpenUtauNotePitchData.Shape shape)
    {
        var input = new List<(long, double)> { lastPoint, thisPoint };
        return shape switch
        {
            OpenUtauNotePitchData.Shape.EaseIn => input.InterpolateCosineEaseIn(SamplingIntervalTick),
            OpenUtauNotePitchData.Shape.EaseOut => input.InterpolateCosineEaseOut(SamplingIntervalTick),
            OpenUtauNotePitchData.Shape.EaseInOut => input.InterpolateCosineEaseInOut(SamplingIntervalTick),
            OpenUtauNotePitchData.Shape.Linear => input.InterpolateLinear(SamplingIntervalTick),
            _ => input.InterpolateCosineEaseInOut(SamplingIntervalTick),
        };
    }

    private static void AppendStartAndEndPoint(List<(long Tick, double Value)> points, Note note)
    {
        long start = note.TickOn;
        long end = note.TickOff;
        bool hasStartPoint = points.Any(p => p.Tick == start);
        bool hasEndPoint = points.Any(p => p.Tick == end);

        if (points.Count <= 1)
        {
            double firstValue = points.Count > 0 ? points[0].Value : 0.0;
            if (!hasStartPoint)
                points.Insert(0, (start, firstValue));
            if (!hasEndPoint)
                points.Add((end, points.Count > 0 ? points[0].Value : 0.0));
            return;
        }

        long firstTick = points[0].Tick;
        long lastTick = points[^1].Tick;

        if (!hasStartPoint)
        {
            if (firstTick > start)
            {
                points.Insert(0, (start, points[0].Value));
            }
            else if (lastTick < start)
            {
                points.Add((start, 0.0));
            }
            else
            {
                var lastPointBefore = points.Last(p => p.Tick < start);
                var firstPointAfter = points.First(p => p.Tick > start);
                double k = (firstPointAfter.Value - lastPointBefore.Value) / (firstPointAfter.Tick - lastPointBefore.Tick);
                double y = lastPointBefore.Value + (start - lastPointBefore.Tick) * k;
                points.Insert(points.IndexOf(firstPointAfter), (start, y));
            }
        }

        if (!hasEndPoint)
        {
            if (firstTick > end)
            {
                points.Insert(0, (end, points[0].Value));
            }
            else if (lastTick < end)
            {
                points.Add((end, 0.0));
            }
            else
            {
                var lastPointBefore = points.Last(p => p.Tick < end);
                var firstPointAfter = points.First(p => p.Tick > end);
                double k = (firstPointAfter.Value - lastPointBefore.Value) / (firstPointAfter.Tick - lastPointBefore.Tick);
                double y = lastPointBefore.Value + (end - lastPointBefore.Tick) * k;
                points.Insert(points.IndexOf(firstPointAfter), (end, y));
            }
        }
    }

    private static List<(long Tick, double Value)> Resampled(
        IReadOnlyList<(long Tick, double Value)> points,
        long interval,
        IReadOnlyList<long>? keyPointPositions = null)
    {
        keyPointPositions ??= new List<long>();
        var keySet = new HashSet<long>(keyPointPositions);
        var grouped = points
            .GroupBy(p => p.Tick / interval * interval)
            .Select(g =>
            {
                var keyPoint = g.FirstOrDefault(p => keySet.Contains(p.Tick));
                if (keySet.Count > 0 && g.Any(p => keySet.Contains(p.Tick)))
                    return (g.Key, keyPoint.Value);
                return (g.Key, g.Average(p => p.Value));
            })
            .OrderBy(p => p.Key)
            .ToList();

        var result = new List<(long, double)>();
        foreach (var point in grouped)
        {
            if (result.Count == 0)
            {
                result.Add((point.Key, point.Item2));
            }
            else
            {
                var input = new List<(long, double)> { result[^1], (point.Key, point.Item2) };
                var interpolated = input.InterpolateLinear(interval);
                result.AddRange(interpolated.Skip(1));
            }
        }

        return result;
    }

    public static List<(long Tick, double Value)> AppendPitchPointsForOpenUtauOutput(IReadOnlyList<(long Tick, double Value)> points) =>
        PitchCalculation.AppendPitchPointsForInterpolation(points, SamplingIntervalTick);
}
