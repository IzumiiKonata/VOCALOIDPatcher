using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsqx;

internal readonly struct VibratoElem
{
    public VibratoElem(int posNrm, int elv)
    {
        PosNrm = posNrm;
        Elv = elv;
    }

    public int PosNrm { get; }
    public int Elv { get; }
}

internal sealed class RateInterval
{
    public RateInterval(double start, double end, bool endClosed, double shift, double omega, double phase)
    {
        Start = start;
        End = end;
        EndClosed = endClosed;
        Shift = shift;
        Omega = omega;
        Phase = phase;
    }

    public double Start { get; }
    public double End { get; }
    public bool EndClosed { get; }
    public double Shift { get; }
    public double Omega { get; }
    public double Phase { get; }

    public bool Contains(double value) =>
        value >= Start && (EndClosed ? value <= End : value < End);

    public double Evaluate(double value) =>
        Math.Cos(Omega * (value - Shift) + Phase);
}

internal sealed class DepthInterval
{
    public DepthInterval(double start, double end, bool endClosed, double value)
    {
        Start = start;
        End = end;
        EndClosed = endClosed;
        Value = value;
    }

    public double Start { get; }
    public double End { get; }
    public bool EndClosed { get; }
    public double Value { get; }

    public bool Contains(double value) =>
        value >= Start && (EndClosed ? value <= End : value < End);
}

internal sealed class VibratoData
{
    public List<RateInterval> RateIntervals { get; } = new();
    public List<DepthInterval> DepthIntervals { get; } = new();

    public bool IsEmpty => RateIntervals.Count == 0 && DepthIntervals.Count == 0;

    public double? RateAt(double secs)
    {
        foreach (var interval in RateIntervals)
            if (interval.Contains(secs))
                return interval.Evaluate(secs);
        return null;
    }

    public double DepthAt(double secs)
    {
        foreach (var interval in DepthIntervals)
            if (interval.Contains(secs))
                return interval.Value;
        return 0;
    }
}

internal static class VsqxVibrato
{
    public static void CollectFromSeqAttr(VibratoData data, string seqId, List<VibratoElem> elems,
        double startSecs, double durationSecs)
    {
        if (elems.Count == 0)
            return;

        if (seqId == "vibRate")
        {
            double phase = 0;
            for (int i = 0; i < elems.Count; i++)
            {
                var prevElem = elems[i];
                bool isLast = i == elems.Count - 1;
                double prevStart = startSecs + durationSecs * prevElem.PosNrm / 65536.0;
                double omega = prevElem.Elv / 2.0;
                double prevEnd;
                if (isLast)
                {
                    prevEnd = startSecs + durationSecs;
                    data.RateIntervals.Add(new RateInterval(prevStart, prevEnd, true, prevStart, omega, phase));
                }
                else
                {
                    prevEnd = startSecs + durationSecs * elems[i + 1].PosNrm / 65536.0;
                    data.RateIntervals.Add(new RateInterval(prevStart, prevEnd, false, prevStart, omega, phase));
                }
                phase += (prevEnd - prevStart) * omega;
            }
        }
        else if (seqId == "vibDep")
        {
            for (int i = 0; i < elems.Count; i++)
            {
                var prevElem = elems[i];
                bool isLast = i == elems.Count - 1;
                double prevStart = startSecs + durationSecs * prevElem.PosNrm / 65536.0;
                double prevEnd;
                if (isLast)
                {
                    prevEnd = startSecs + durationSecs;
                    data.DepthIntervals.Add(new DepthInterval(prevStart, prevEnd, true, prevElem.Elv));
                }
                else
                {
                    prevEnd = startSecs + durationSecs * elems[i + 1].PosNrm / 65536.0;
                    data.DepthIntervals.Add(new DepthInterval(prevStart, prevEnd, false, prevElem.Elv));
                }
            }
        }
    }

    public static ParamCurve Apply(ParamCurve pitch, VibratoData data, TimeSynchronizer synchronizer,
        int offset, int partStartTick, int partEndTick)
    {
        if (data.IsEmpty || pitch.Points.Count == 0)
            return pitch;

        var newPoints = new List<Point>(pitch.Points.Count);
        foreach (var point in pitch.Points)
        {
            int tick = point.X;
            int relative = tick - offset;
            if (relative < partStartTick || relative >= partEndTick)
            {
                newPoints.Add(point);
                continue;
            }

            double secs = synchronizer.GetActualSecsFromTicks(tick);
            double depth = data.DepthAt(secs);
            double? rate = data.RateAt(secs);
            if (depth != 0 && rate.HasValue && rate.Value != 0)
            {
                double vibratoOffset = depth * rate.Value;
                newPoints.Add(point.WithY(point.Y + (int)Math.Round(vibratoOffset)));
            }
            else
            {
                newPoints.Add(point);
            }
        }
        pitch.Points = newPoints;
        return pitch;
    }
}
