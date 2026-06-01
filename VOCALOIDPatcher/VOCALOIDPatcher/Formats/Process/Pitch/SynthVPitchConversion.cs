using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record SvpDefaultVibratoParameters(
    double? VibratoStart,
    double? EaseInLength,
    double? EaseOutLength,
    double? Depth,
    double? Frequency);

public sealed record SvpNoteWithVibrato(
    long NoteStartTick,
    long NoteLengthTick,
    double? VibratoStart,
    double? EaseInLength,
    double? EaseOutLength,
    double? Depth,
    double? Frequency,
    double? Phase)
{
    public long NoteEndTick => NoteStartTick + NoteLengthTick;
}

public static class SynthVPitchConversion
{
    private const long SamplingIntervalTick = 4L;
    private const double VibratoDefaultStartSec = 0.25;
    private const double VibratoDefaultEaseInSec = 0.2;
    private const double VibratoDefaultEaseOutSec = 0.2;
    private const double VibratoDefaultDepthSemitone = 1.0;
    private const double VibratoDefaultFrequencyHz = 5.5;
    private const double VibratoDefaultPhaseRad = 0.0;

    public static List<(long Tick, double Value)> ProcessSvpInputPitchData(
        IReadOnlyList<(long Tick, double Value)> points,
        string? mode,
        IReadOnlyList<SvpNoteWithVibrato> notesWithVibrato,
        IReadOnlyList<Tempo> tempos,
        IReadOnlyList<(long Tick, double Value)> vibratoEnvPoints,
        string? vibratoEnvMode,
        SvpDefaultVibratoParameters? vibratoDefaultParameters)
    {
        var pitch = points.Merge().Interpolate(mode);
        var env = vibratoEnvPoints.Merge().Interpolate(vibratoEnvMode).ExtendEveryTick();
        return pitch
            .AppendVibrato(notesWithVibrato, vibratoDefaultParameters, tempos, env)
            .RemoveRedundantPoints();
    }

    private static List<(long Tick, double Value)> Merge(this IReadOnlyList<(long Tick, double Value)> points) =>
        points
            .GroupBy(p => p.Tick)
            .Select(g => (g.Key, g.Average(p => p.Value)))
            .OrderBy(p => p.Key)
            .ToList();

    private static List<(long Tick, double Value)> Interpolate(this IReadOnlyList<(long Tick, double Value)> points, string? mode) =>
        mode switch
        {
            "linear" => points.InterpolateLinear(SamplingIntervalTick),
            "cosine" => points.InterpolateCosineEaseInOut(SamplingIntervalTick),
            "cubic" => points.InterpolateCosineEaseInOut(SamplingIntervalTick),
            _ => points.InterpolateCosineEaseInOut(SamplingIntervalTick),
        };

    private static Dictionary<long, double> ExtendEveryTick(this IReadOnlyList<(long Tick, double Value)> points)
    {
        var acc = new List<(long Tick, double Value)>();
        foreach (var point in points)
        {
            if (acc.Count == 0 || acc[^1].Value == 1.0)
            {
                acc.Add(point);
            }
            else
            {
                var last = acc[^1];
                for (long t = last.Tick; t < point.Tick; t++)
                    acc.Add((t, last.Value));
                acc.Add(point);
            }
        }

        var map = new Dictionary<long, double>();
        foreach (var (tick, value) in acc)
            map[tick] = value;
        return map;
    }

    private static List<(long Tick, double Value)> AppendVibrato(
        this IReadOnlyList<(long Tick, double Value)> pitchPoints,
        IReadOnlyList<SvpNoteWithVibrato> notes,
        SvpDefaultVibratoParameters? vibratoDefaultParameters,
        IReadOnlyList<Tempo> tempos,
        Dictionary<long, double> vibratoEnv)
    {
        var transformer = new TickTimeTransformer(tempos);

        var ranges = new List<((long Start, long End) Range, SvpNoteWithVibrato? Note)>();
        long lastTick = 0L;
        foreach (var note in notes)
        {
            if (lastTick < note.NoteStartTick)
                ranges.Add(((lastTick, note.NoteStartTick), null));
            ranges.Add(((note.NoteStartTick, note.NoteEndTick), note));
            lastTick = note.NoteEndTick;
        }

        ranges.Add(((lastTick, long.MaxValue), null));

        var result = new List<(long, double)>();
        int pitchIndex = 0;
        foreach (var (range, note) in ranges)
        {
            while (pitchIndex < pitchPoints.Count && pitchPoints[pitchIndex].Tick < range.Start)
                pitchIndex++;
            int startIndex = pitchIndex;
            while (pitchIndex < pitchPoints.Count && pitchPoints[pitchIndex].Tick >= range.Start && pitchPoints[pitchIndex].Tick < range.End)
                pitchIndex++;
            if (startIndex < pitchIndex)
            {
                var subset = new List<(long, double)>();
                for (int i = startIndex; i < pitchIndex; i++)
                    subset.Add(pitchPoints[i]);
                result.AddRange(subset.AppendVibratoInNote(note, vibratoDefaultParameters, transformer, tempos, vibratoEnv));
            }
        }

        return result;
    }

    private static List<(long Tick, double Value)> AppendVibratoInNote(
        this List<(long Tick, double Value)> points,
        SvpNoteWithVibrato? note,
        SvpDefaultVibratoParameters? defaultParameters,
        TickTimeTransformer transformer,
        IReadOnlyList<Tempo> tempos,
        Dictionary<long, double> vibratoEnv)
    {
        if (note == null || note.NoteStartTick < 0L)
            return points;

        double noteStartSec = transformer.TickToSec(note.NoteStartTick);
        double noteEndSec = transformer.TickToSec(note.NoteEndTick);

        double vibratoStartSec = (note.VibratoStart ?? defaultParameters?.VibratoStart ?? VibratoDefaultStartSec) + noteStartSec;
        long vibratoStartTick = transformer.SecToTick(vibratoStartSec);
        double easeInLength = note.EaseInLength ?? defaultParameters?.EaseInLength ?? VibratoDefaultEaseInSec;
        double easeOutLength = note.EaseOutLength ?? defaultParameters?.EaseOutLength ?? VibratoDefaultEaseOutSec;
        double depth = (note.Depth ?? defaultParameters?.Depth ?? VibratoDefaultDepthSemitone) * 0.5;
        if (depth == 0.0)
            return points;
        double phase = note.Phase ?? VibratoDefaultPhaseRad;
        double frequency = note.Frequency ?? defaultParameters?.Frequency ?? VibratoDefaultFrequencyHz;

        double bpm = tempos.LastOrDefault(t => t.TickPosition <= note.NoteStartTick)?.Bpm ?? Constants.DefaultBpm;
        double secPerTick = bpm.BpmToSecPerTick();

        double Vibrato(long tick)
        {
            double sec = transformer.TickToSec(tick);
            if (sec < vibratoStartSec)
                return 0.0;
            double easeInFactor = Math.Clamp((sec - vibratoStartSec) / easeInLength, 0.0, 1.0);
            double easeOutFactor = Math.Clamp((noteEndSec - sec) / easeOutLength, 0.0, 1.0);
            double rad = 2 * Math.PI * frequency * secPerTick * (tick - vibratoStartTick) + phase;
            double envelope = vibratoEnv.TryGetValue(tick, out var e) ? e : 1.0;
            return envelope * depth * easeInFactor * easeOutFactor * Math.Sin(rad);
        }

        var basePoints = points.Count > 0
            ? points
            : new List<(long Tick, double Value)> { (note.NoteStartTick, 0.0), (note.NoteEndTick, 0.0) };

        List<(long Tick, double Value)> ordered;
        if (basePoints[^1].Tick != note.NoteEndTick)
        {
            ordered = basePoints.ToList();
            ordered.Add((note.NoteEndTick, basePoints[^1].Value));
        }
        else
        {
            ordered = basePoints;
        }

        var result = new List<(long, double)>();
        (long Tick, double Value)? prev = null;
        foreach (var point in ordered)
        {
            if (prev == null)
            {
                result.Add((point.Tick, point.Value + Vibrato(point.Tick)));
            }
            else
            {
                long tick = prev.Value.Tick + SamplingIntervalTick;
                while (tick < point.Tick)
                {
                    result.Add((tick, prev.Value.Value + Vibrato(tick)));
                    tick += SamplingIntervalTick;
                }

                result.Add((point.Tick, point.Value + Vibrato(point.Tick)));
            }

            prev = point;
        }

        return result;
    }

    private static List<(long Tick, double Value)> RemoveRedundantPoints(this IReadOnlyList<(long Tick, double Value)> points)
    {
        var acc = new List<(long, double)>();
        foreach (var point in points)
        {
            double? previousValue = acc.Count > 0 ? acc[^1].Item2 : (double?)null;
            if (point.Value != previousValue)
                acc.Add(point);
        }

        return acc;
    }

    public static List<(long Tick, double Value)> AppendPitchPointsForSvpOutput(this IReadOnlyList<(long Tick, double Value)> points) =>
        PitchCalculation.AppendPitchPointsForInterpolation(points, SamplingIntervalTick).ReduceRepeatedPitchPoints();
}
