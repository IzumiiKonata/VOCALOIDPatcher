using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record UtauNoteVibratoParams(
    double Length,
    double Period,
    double Depth,
    double FadeIn,
    double FadeOut,
    double PhaseShift,
    double Shift);

public static class UtauVibratoConversion
{
    public static List<(long Tick, double Value)> AppendUtauNoteVibrato(
        this IReadOnlyList<(long Tick, double Value)> points,
        UtauNoteVibratoParams? vibratoParams,
        Note thisNote,
        TickTimeTransformer transformer,
        long sampleIntervalTick)
    {
        var list = new List<(long, double)>(points);
        if (vibratoParams == null)
            return list;

        double noteLength = transformer.TickDistanceToMilliSec(thisNote.TickOn, thisNote.TickOff);
        double vibratoLength = noteLength * vibratoParams.Length / 100;
        if (vibratoLength <= 0)
            return list;
        double frequency = 1.0 / vibratoParams.Period;
        if (double.IsInfinity(frequency) || double.IsNaN(frequency))
            return list;
        double depth = vibratoParams.Depth / 100;
        if (depth <= 0)
            return list;
        double easeInLength = noteLength * vibratoParams.FadeIn / 100;
        double easeOutLength = noteLength * vibratoParams.FadeOut / 100;
        double phase = vibratoParams.PhaseShift / 100;
        double shift = vibratoParams.Shift / 100;

        double start = noteLength - vibratoLength;

        double Vibrato(double t)
        {
            if (t < start)
                return 0.0;
            double easeInFactor = FiniteOrOne(Math.Clamp((t - start) / easeInLength, 0.0, 1.0));
            double easeOutFactor = FiniteOrOne(Math.Clamp((noteLength - t) / easeOutLength, 0.0, 1.0));
            double x = 2 * Math.PI * (frequency * (t - start) - phase);
            return depth * easeInFactor * easeOutFactor * (Math.Sin(x) + shift);
        }

        double noteStartInMillis = transformer.TickToMilliSec(thisNote.TickOn);
        double sampleIntervalInMillis = transformer.TickDistanceToMilliSec(thisNote.TickOn, thisNote.TickOn + sampleIntervalTick);

        var acc = new List<(double Ms, double Value)>();
        foreach (var (tick, value) in list)
        {
            double inputMs = transformer.TickToMilliSec(tick) - noteStartInMillis;
            var newPoint = (inputMs, value + Vibrato(inputMs));
            if (acc.Count == 0)
            {
                acc.Add(newPoint);
            }
            else
            {
                var lastPoint = acc[^1];
                double pos = lastPoint.Ms + sampleIntervalInMillis;
                while (pos < newPoint.Item1)
                {
                    acc.Add((pos, lastPoint.Value + Vibrato(pos)));
                    pos += sampleIntervalInMillis;
                }

                acc.Add(newPoint);
            }
        }

        var result = new List<(long, double)>(acc.Count);
        foreach (var (ms, value) in acc)
            result.Add((transformer.MilliSecToTick(ms + noteStartInMillis), value));
        return result;
    }

    private static double FiniteOrOne(double value) =>
        double.IsInfinity(value) || double.IsNaN(value) ? 1.0 : value;
}
