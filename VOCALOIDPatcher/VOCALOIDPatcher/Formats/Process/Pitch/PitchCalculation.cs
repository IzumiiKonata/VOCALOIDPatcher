using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Exceptions;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public static class PitchCalculation
{
    public static double LoggedFrequencyToKey(this double value) =>
        Constants.KeyCenterC + (value - Constants.LogFrqCenterC) / Constants.LogFrqDiffOneKey;

    public static double KeyToLoggedFrequency(this double value) =>
        (value - Constants.KeyCenterC) * Constants.LogFrqDiffOneKey + Constants.LogFrqCenterC;

    public static IReadOnlyList<(long Tick, double? Value)>? GetAbsoluteData(this Model.Pitch pitch, IReadOnlyList<Note> notes) =>
        pitch.ConvertRelativity(notes, toAbsolute: true);

    public static IReadOnlyList<(long Tick, double Value)>? GetRelativeData(
        this Model.Pitch pitch,
        IReadOnlyList<Note> notes,
        long borderAppendRadius = 0L)
    {
        var converted = pitch.ConvertRelativity(notes, toAbsolute: false, borderAppendRadius);
        return converted?
            .Where(p => p.Value.HasValue)
            .Select(p => (p.Tick, p.Value!.Value))
            .ToList();
    }

    private static IReadOnlyList<(long Tick, double? Value)>? ConvertRelativity(
        this Model.Pitch pitch,
        IReadOnlyList<Note> notes,
        bool toAbsolute,
        long borderAppendRadius = 0L)
    {
        if (pitch.IsAbsolute && toAbsolute)
            return pitch.Data;
        if (!pitch.IsAbsolute && !toAbsolute)
            return pitch.Data;
        if (notes.Count == 0)
            return null;

        var borders = notes.GetBorders();
        int index = 0;
        int currentNoteKey = notes[0].Key;
        long nextBorder = borders.Count > 0 ? borders[0] : long.MaxValue;

        var mapped = new List<(long, double?)>(pitch.Data.Count);
        foreach (var (pos, value) in pitch.Data)
        {
            while (pos >= nextBorder)
            {
                index++;
                nextBorder = index < borders.Count ? borders[index] : long.MaxValue;
                currentNoteKey = notes[index].Key;
            }

            double? convertedValue;
            if (value.HasValue)
            {
                if (pitch.IsAbsolute)
                    convertedValue = value.Value - currentNoteKey;
                else
                    convertedValue = value.Value == 0.0 ? null : value.Value + currentNoteKey;
            }
            else
            {
                convertedValue = 0.0;
            }

            mapped.Add((pos, convertedValue));
        }

        return toAbsolute ? mapped : mapped.AppendPointsAtBorders(notes, borderAppendRadius);
    }

    private static List<long> GetBorders(this IReadOnlyList<Note> notes)
    {
        var borders = new List<long>();
        long pos = -1L;
        foreach (var note in notes)
        {
            if (pos < 0)
            {
                pos = note.TickOff;
                continue;
            }

            if (pos == note.TickOn)
                borders.Add(pos);
            else if (pos < note.TickOn)
                borders.Add((note.TickOn + pos) / 2);
            else
                throw new NotesOverlappingException();

            pos = note.TickOff;
        }

        return borders;
    }

    private static List<(long Tick, double? Value)> AppendPointsAtBorders(
        this List<(long Tick, double? Value)> data,
        IReadOnlyList<Note> notes,
        long radius)
    {
        if (radius <= 0)
            return data;

        var result = data.ToList();
        for (int i = 0; i < notes.Count - 1; i++)
        {
            var lastNote = notes[i];
            var thisNote = notes[i + 1];
            if (thisNote.TickOn - lastNote.TickOff > radius)
                continue;

            int firstIndex = result.FindIndex(p => p.Tick >= thisNote.TickOn);
            if (firstIndex < 0)
                continue;

            var firstPoint = result[firstIndex];
            if (firstPoint.Tick == thisNote.TickOn || firstPoint.Tick - thisNote.TickOn > radius)
                continue;

            if (!firstPoint.Value.HasValue)
                continue;

            long newPointTick = thisNote.TickOn - radius;
            var newPoint = (newPointTick, firstPoint.Value);
            result.Insert(firstIndex, newPoint);
            result.RemoveAll(p => p.Tick >= newPointTick && p.Tick < thisNote.TickOn && p != newPoint);
        }

        return result;
    }

    public static List<(long Tick, double Value)> AppendPitchPointsForInterpolation(
        IReadOnlyList<(long Tick, double Value)> points,
        long intervalTick)
    {
        var result = new List<(long, double)>();
        if (points.Count > 0)
            result.Add(points[0]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var lastPoint = points[i];
            var thisPoint = points[i + 1];
            long tickDiff = thisPoint.Tick - lastPoint.Tick;

            if (tickDiff >= intervalTick)
            {
                if (tickDiff < 2 * intervalTick)
                    result.Add(((thisPoint.Tick + lastPoint.Tick) / 2, lastPoint.Value));
                else
                    result.Add((thisPoint.Tick - intervalTick, lastPoint.Value));
            }

            result.Add(thisPoint);
        }

        return result;
    }

    public static List<(long Tick, double Value)> ReduceRepeatedPitchPoints(this IReadOnlyList<(long Tick, double Value)> points)
    {
        var toRemove = new HashSet<int>();
        double? currentRepeatedValue = null;
        int prevIndex = -1;

        for (int i = 0; i < points.Count; i++)
        {
            if (prevIndex < 0)
            {
                prevIndex = i;
                continue;
            }

            if (currentRepeatedValue == null)
            {
                if (points[prevIndex].Value == points[i].Value)
                    currentRepeatedValue = points[i].Value;
                prevIndex = i;
                continue;
            }

            if (currentRepeatedValue == points[i].Value)
                toRemove.Add(prevIndex);
            else
                currentRepeatedValue = null;
            prevIndex = i;
        }

        var result = new List<(long, double)>();
        for (int i = 0; i < points.Count; i++)
            if (!toRemove.Contains(i))
                result.Add(points[i]);
        return result;
    }
}
