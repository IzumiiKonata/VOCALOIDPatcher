using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Io;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record DvSegmentPitchRawData(long TickOffset, IReadOnlyList<(int Tick, int Value)> Data);

public sealed record DvNoteWithPitch(
    Note Note,
    int PorHead,
    int PorTail,
    int BenLen,
    int BenDep,
    IReadOnlyList<(int Ms, int MinusCent)> Vibrato) : IRichNote<DvNoteWithPitch>
{
    public DvNoteWithPitch CopyWithNote(Note note) => this with { Note = note };
}

public static class DeepVocalPitchConversion
{
    private const long SamplingIntervalTick = 4L;
    private const double PortamentoLengthMaxSec = 0.3125;
    private const double BendDownLengthFixedSec = 0.09375;
    private const double BendLengthMinSec = 0.375;
    private const double BendLengthMaxSec = 0.6875;
    private const double BendValueMax = 3.0;

    public static Model.Pitch? PitchFromDvTrack(IReadOnlyList<DvSegmentPitchRawData> segments, IReadOnlyList<DvNoteWithPitch> notes, IReadOnlyList<Tempo> tempos)
    {
        var merged = MergeSameTickPoints(MergePointsFromSegments(segments));
        if (merged == null)
            return null;
        var data = ApplyDefaultPitch(MergeSameValuePoints(merged), notes, tempos);
        return new Model.Pitch(data, true);
    }

    private static List<(long Tick, double? Value)> MergePointsFromSegments(IReadOnlyList<DvSegmentPitchRawData> segments)
    {
        var result = new List<(long, double?)>();
        foreach (var segment in segments)
        {
            foreach (var (rawTick, centValue) in segment.Data)
            {
                if (rawTick < 0)
                    continue;
                long tick = rawTick + segment.TickOffset;
                double? value = centValue < 0 ? null : Dv.ConvertNoteKey(centValue / 100.0);
                result.Add((tick, value));
            }
        }

        return result;
    }

    private static List<(long Tick, double? Value)>? MergeSameTickPoints(IReadOnlyList<(long Tick, double? Value)> points)
    {
        var merged = points
            .GroupBy(p => p.Tick)
            .Select(g =>
            {
                var list = g.ToList();
                if (list.Count > 1)
                {
                    if (list.Any(p => p.Value == null))
                        return (g.Key, (double?)null);
                    return (g.Key, (double?)list.Where(p => p.Value.HasValue).Average(p => p.Value!.Value));
                }

                return (g.Key, list[0].Value);
            })
            .OrderBy(p => p.Key)
            .Select(p => (p.Key, p.Item2))
            .ToList();
        return merged.Any(p => p.Item2 != null) ? merged : null;
    }

    private static List<(long Tick, double? Value)> MergeSameValuePoints(IReadOnlyList<(long Tick, double? Value)> points)
    {
        var acc = new List<(long, double?)>();
        foreach (var point in points.OrderBy(p => p.Tick))
        {
            double? lastValue = acc.Count > 0 ? acc[^1].Item2 : null;
            if (point.Value != lastValue || acc.Count == 0)
                acc.Add(point);
        }

        return acc;
    }

    private static List<(long Tick, double? Value)> ApplyDefaultPitch(List<(long Tick, double? Value)> points, IReadOnlyList<DvNoteWithPitch> notes, IReadOnlyList<Tempo> tempos)
    {
        if (points.Count == 0 || notes.Count == 0)
            return points;

        var transformer = new TickTimeTransformer(tempos);
        var basePitch = GetBasePitch(notes, transformer);
        var bendDiff = GetBendPitch(notes, transformer);
        var vibratoDiff = GetVibratoPitch(notes, transformer);

        var input = new List<(long Tick, double? Value)>(points);
        if (points[^1].Tick < notes[^1].Note.TickOff)
            input.Add((notes[^1].Note.TickOff, null));

        var acc = new List<(long Tick, double? Value)>();
        foreach (var point in input)
        {
            (long Tick, double? Value)? lastPoint = acc.Count > 0 ? acc[^1] : null;
            long startTick = lastPoint?.Tick ?? 0;
            long endTick = point.Tick;

            if (lastPoint == null || lastPoint.Value.Value == null)
            {
                for (long t = startTick; t < endTick; t += SamplingIntervalTick)
                {
                    double value = (basePitch.TryGetValue(t, out var b) ? b : 0.0)
                                   + (bendDiff.TryGetValue(t, out var bd) ? bd : 0.0)
                                   + (vibratoDiff.TryGetValue(t, out var vd) ? vd : 0.0);
                    acc.Add((t, value));
                }

                acc.Add(point);
            }
            else
            {
                acc.Add(point);
            }
        }

        return acc;
    }

    private static Dictionary<long, double> GetBasePitch(IReadOnlyList<DvNoteWithPitch> notes, TickTimeTransformer transformer)
    {
        var sequence = new List<DvNoteWithPitch?> { null };
        sequence.AddRange(notes);
        sequence.Add(null);

        var all = new List<(long Tick, double? Value)>();
        for (int i = 0; i < sequence.Count - 1; i++)
        {
            var lastNote = sequence[i];
            var thisNote = sequence[i + 1];
            var result = new List<(long, double?)>();

            var portamento = lastNote != null && thisNote != null
                ? GetPortamento(lastNote, transformer, thisNote)
                : new List<(long, double)>();
            foreach (var p in portamento)
                result.Add((p.Item1, p.Item2));

            if (lastNote != null)
            {
                long tail = portamento.Count > 0 ? portamento[0].Item1 : lastNote.Note.TickOff;
                for (long t = TickHalfStart(lastNote.Note); t < tail; t++)
                    result.Add((t, (double)lastNote.Note.Key));
            }

            if (thisNote != null)
            {
                long start = lastNote == null ? 0 : (portamento.Count > 0 ? portamento[^1].Item1 : thisNote.Note.TickOn);
                for (long t = start; t < TickHalfStart(thisNote.Note); t++)
                    result.Add((t, (double)thisNote.Note.Key));
            }

            all.AddRange(result);
        }

        var merged = MergeSameTickPoints(all) ?? new List<(long, double?)>();
        var map = new Dictionary<long, double>();
        foreach (var (tick, value) in merged)
            if (value.HasValue)
                map[tick] = value.Value;
        return map;
    }

    private static Dictionary<long, double> GetBendPitch(IReadOnlyList<DvNoteWithPitch> notes, TickTimeTransformer transformer)
    {
        var all = new List<(long, double?)>();
        foreach (var note in notes)
        {
            long startTick = note.Note.TickOn;
            double startSec = transformer.TickToSec(startTick);
            double valleySec = startSec + BendDownLengthFixedSec;
            long valleyTick = Math.Min(transformer.SecToTick(valleySec), note.Note.TickOn + note.Note.Length / 2 - 1);

            double lengthSec = note.BenLen <= 50
                ? BendLengthMinSec
                : (BendLengthMaxSec - BendLengthMinSec) * (note.BenLen - 50) / 50 + BendLengthMinSec;
            double endSec = startSec + lengthSec;
            long endTick = Math.Min(transformer.SecToTick(endSec), note.Note.TickOff - 1);

            double valleyValue = -BendValueMax * note.BenDep / 100;
            var valleyPoint = (valleyTick, valleyValue);

            var bendDown = new List<(long, double)> { (startTick, 0.0), valleyPoint }.InterpolateLinear(1L);
            var bendUp = new List<(long, double)> { valleyPoint, (endTick, 0.0) }.InterpolateCosineEaseInOut(1L).Skip(1);

            foreach (var p in bendDown)
                all.Add((p.Tick, p.Value));
            foreach (var p in bendUp)
                all.Add((p.Tick, p.Value));
        }

        return ToMap(MergeSameTickPoints(all));
    }

    private static List<(long, double)> GetPortamento(DvNoteWithPitch lastNote, TickTimeTransformer transformer, DvNoteWithPitch thisNote)
    {
        double tailLengthSec = PortamentoLengthMaxSec * lastNote.PorTail / 100;
        double startSec = transformer.TickToSec(lastNote.Note.TickOff) - tailLengthSec;
        long startTick = Math.Max(transformer.SecToTick(startSec), TickHalfStart(lastNote.Note));

        double headLengthSec = PortamentoLengthMaxSec * thisNote.PorHead / 100;
        double endSec = transformer.TickToSec(thisNote.Note.TickOn) + headLengthSec;
        long endTick = Math.Min(transformer.SecToTick(endSec), TickHalfStart(thisNote.Note) - 1);

        return new List<(long, double)> { (startTick, lastNote.Note.Key), (endTick, thisNote.Note.Key) }.InterpolateCosineEaseInOut(1L);
    }

    private static Dictionary<long, double> GetVibratoPitch(IReadOnlyList<DvNoteWithPitch> notes, TickTimeTransformer transformer)
    {
        var all = new List<(long, double?)>();
        foreach (var note in notes)
        {
            long startTick = note.Note.TickOn;
            double startSec = transformer.TickToSec(startTick);
            var points = note.Vibrato
                .Select(v => (Tick: transformer.SecToTick(startSec + v.Ms / 1000.0), Value: -v.MinusCent / 100.0))
                .Where(p => p.Tick >= startTick && p.Tick < note.Note.TickOff)
                .OrderBy(p => p.Tick)
                .ToList();
            foreach (var p in points.InterpolateLinear(1L))
                all.Add((p.Tick, p.Value));
        }

        return ToMap(MergeSameTickPoints(all));
    }

    private static Dictionary<long, double> ToMap(List<(long Tick, double? Value)>? points)
    {
        var map = new Dictionary<long, double>();
        if (points == null)
            return map;
        foreach (var (tick, value) in points)
            if (value.HasValue)
                map[tick] = value.Value;
        return map;
    }

    private static long TickHalfStart(Note note) => note.TickOn + (note.Length + 1) / 2;

    public static DvSegmentPitchRawData? GenerateForDv(this Model.Pitch pitch, IReadOnlyList<Note> notes)
    {
        if (notes.Count == 0)
            return null;
        var points = pitch.GetAbsoluteData(notes);
        if (points == null || points.Count == 0)
            return null;

        var data = new List<(int, int)> { (-1, -1) };
        foreach (var (tick, value) in AppendPoints(points))
        {
            int rawValue = value.HasValue ? (int)Math.Round(Dv.ConvertNoteKey(value.Value) * 100) : -1;
            data.Add(((int)tick, rawValue));
        }

        return new DvSegmentPitchRawData(0L, data);
    }

    private static List<(long Tick, double? Value)> AppendPoints(IReadOnlyList<(long Tick, double? Value)> points)
    {
        var results = new List<(long, double?)>();
        double? lastValue = null;
        foreach (var point in points)
        {
            if (lastValue == null && point.Value != null)
                results.Add((point.Tick, null));
            if (lastValue != null && point.Value == null)
                results.Add((point.Tick, lastValue));
            results.Add(point);
            lastValue = point.Value;
        }

        return results;
    }
}
