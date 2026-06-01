using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public static class TickTimeExtensions
{
    public static double BpmToSecPerTick(this double bpm) => 60.0 / Constants.TicksInBeat / bpm;
}

public sealed class TickTimeTransformer
{
    private sealed class Segment
    {
        public Segment(long start, long end, double offset, double secPerTick)
        {
            Start = start;
            End = end;
            Offset = offset;
            SecPerTick = secPerTick;
        }

        public long Start { get; }
        public long End { get; }
        public double Offset { get; }
        public double SecPerTick { get; }

        public bool Contains(long tick) => tick >= Start && tick < End;
    }

    private readonly List<Segment> _segments = new();

    public TickTimeTransformer(IReadOnlyList<Tempo> tempos)
    {
        for (int i = 0; i < tempos.Count; i++)
        {
            var thisTempo = tempos[i];
            var nextTempo = i + 1 < tempos.Count ? tempos[i + 1] : (Tempo?)null;
            long start = thisTempo.TickPosition;
            long end = nextTempo?.TickPosition ?? long.MaxValue;
            double rate = thisTempo.Bpm.BpmToSecPerTick();

            if (_segments.Count == 0)
            {
                _segments.Add(new Segment(start, end, 0.0, rate));
            }
            else
            {
                var last = _segments[^1];
                double offset = last.Offset + (last.End - last.Start) * last.SecPerTick;
                _segments.Add(new Segment(start, end, offset, rate));
            }
        }
    }

    public double TickToSec(long tick)
    {
        var segment = _segments.FirstOrDefault(s => s.Contains(tick)) ?? _segments[0];
        return segment.Offset + (tick - segment.Start) * segment.SecPerTick;
    }

    public double TickToMilliSec(long tick) => TickToSec(tick) * 1000;

    public double TickDistanceToSec(long tickStart, long tickEnd) => TickToSec(tickEnd) - TickToSec(tickStart);

    public double TickDistanceToMilliSec(long tickStart, long tickEnd) => TickDistanceToSec(tickStart, tickEnd) * 1000;

    public long SecToTick(double sec)
    {
        Segment? found = null;
        foreach (var s in _segments)
            if (s.Offset <= sec)
                found = s;
        found ??= _segments[0];
        return (long)((sec - found.Offset) / found.SecPerTick) + found.Start;
    }

    public long MilliSecToTick(double milliSec) => SecToTick(milliSec / 1000.0);
}
