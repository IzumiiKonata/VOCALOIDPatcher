using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.Process;

public static class Interpolation
{
    public static List<(long Tick, double Value)> InterpolateLinear(this IReadOnlyList<(long Tick, double Value)> points, long samplingIntervalTick) =>
        points.Interpolate(samplingIntervalTick, (start, end, indexes) =>
        {
            var result = new List<(long, double)>(indexes.Count);
            foreach (var x in indexes)
            {
                double y = start.Value + (x - start.Tick) * (end.Value - start.Value) / (double)(end.Tick - start.Tick);
                result.Add((x, y));
            }

            return result;
        });

    public static List<(long Tick, double Value)> InterpolateCosineEaseInOut(this IReadOnlyList<(long Tick, double Value)> points, long samplingIntervalTick) =>
        points.Interpolate(samplingIntervalTick, (start, end, indexes) =>
        {
            double yOffset = (start.Value + end.Value) / 2;
            double aFreq = Math.PI / (end.Tick - start.Tick);
            double amp = (start.Value - end.Value) / 2;
            var result = new List<(long, double)>(indexes.Count);
            foreach (var x in indexes)
                result.Add((x, amp * Math.Cos(aFreq * (x - start.Tick)) + yOffset));
            return result;
        });

    public static List<(long Tick, double Value)> InterpolateCosineEaseIn(this IReadOnlyList<(long Tick, double Value)> points, long samplingIntervalTick) =>
        points.Interpolate(samplingIntervalTick, (start, end, indexes) =>
        {
            double yOffset = end.Value;
            double aFreq = Math.PI / (end.Tick - start.Tick) / 2;
            double amp = start.Value - end.Value;
            var result = new List<(long, double)>(indexes.Count);
            foreach (var x in indexes)
                result.Add((x, amp * Math.Cos(aFreq * (x - start.Tick)) + yOffset));
            return result;
        });

    public static List<(long Tick, double Value)> InterpolateCosineEaseOut(this IReadOnlyList<(long Tick, double Value)> points, long samplingIntervalTick) =>
        points.Interpolate(samplingIntervalTick, (start, end, indexes) =>
        {
            double yOffset = start.Value;
            double aFreq = Math.PI / (end.Tick - start.Tick) / 2;
            double amp = start.Value - end.Value;
            double phase = Math.PI / 2;
            var result = new List<(long, double)>(indexes.Count);
            foreach (var x in indexes)
                result.Add((x, amp * Math.Cos(aFreq * (x - start.Tick) + phase) + yOffset));
            return result;
        });

    private static List<(long Tick, double Value)> Interpolate(
        this IReadOnlyList<(long Tick, double Value)> points,
        long samplingIntervalTick,
        Func<(long Tick, double Value), (long Tick, double Value), List<long>, List<(long, double)>> mapping)
    {
        if (points.Count == 0)
            return new List<(long, double)>();

        var result = new List<(long, double)>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var indexes = new List<long>();
            for (long x = start.Tick + 1; x < end.Tick; x++)
                if ((x - start.Tick) % samplingIntervalTick == 0L)
                    indexes.Add(x);

            result.Add(start);
            result.AddRange(mapping(start, end, indexes));
        }

        result.Add(points[^1]);
        return result;
    }
}
