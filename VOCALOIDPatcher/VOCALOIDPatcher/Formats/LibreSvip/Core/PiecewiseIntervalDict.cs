using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public sealed class PiecewiseIntervalDict
{
    private readonly struct Segment
    {
        public readonly double Start;
        public readonly double End;
        public readonly Func<double, double> Value;

        public Segment(double start, double end, Func<double, double> value)
        {
            Start = start;
            End = end;
            Value = value;
        }
    }

    private readonly List<Segment> _segments = new();

    public void SetConstant(double start, double end, double value) =>
        Set(start, end, _ => value);

    public void Set(double start, double end, Func<double, double> value)
    {
        if (end <= start)
            return;
        var next = new List<Segment>();
        foreach (var seg in _segments)
        {
            if (seg.End <= start || seg.Start >= end)
            {
                next.Add(seg);
                continue;
            }
            if (seg.Start < start)
                next.Add(new Segment(seg.Start, start, seg.Value));
            if (seg.End > end)
                next.Add(new Segment(end, seg.End, seg.Value));
        }
        next.Add(new Segment(start, end, value));
        next.Sort((a, b) => a.Start.CompareTo(b.Start));
        _segments.Clear();
        _segments.AddRange(next);
    }

    public double? Get(double x)
    {
        int lo = 0, hi = _segments.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (x < _segments[mid].Start)
                hi = mid;
            else
                lo = mid + 1;
        }
        int idx = lo - 1;
        if (idx >= 0 && x < _segments[idx].End)
            return _segments[idx].Value(x);
        return null;
    }
}
