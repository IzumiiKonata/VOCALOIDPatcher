using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ust;

public static class UstResampling
{
    public static List<Point> Resampled(
        List<Point> data, int interval, Func<Point, Point, int, double> interpolateMethod)
    {
        var result = new List<Point>();
        if (data.Count == 0)
            return result;
        int left = data[0].X;
        int right = data[0].X;
        foreach (var p in data)
        {
            if (p.X < left)
                left = p.X;
            if (p.X > right)
                right = p.X;
        }
        for (int current = left; current <= right; current += interval)
        {
            int prevIndex = Search.FindLastIndex(data, p => p.X <= current);
            int nextIndex = Search.FindIndex(data, p => p.X >= current);
            if (prevIndex == -1 || nextIndex == -1)
                continue;
            result.Add(new Point(current, (int)interpolateMethod(data[prevIndex], data[nextIndex], current)));
        }
        return result;
    }

    public static List<Point> DotResampled(List<Point> data, int interval) =>
        Resampled(data, interval, (prev, next, _) => prev.Y);
}
