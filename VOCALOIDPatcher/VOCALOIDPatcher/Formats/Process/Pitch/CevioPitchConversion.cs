using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Process.Pitch;

public sealed record CevioTrackPitchData(
    IReadOnlyList<CevioTrackPitchData.Event> Events,
    IReadOnlyList<Tempo> Tempos,
    long TickPrefix)
{
    public sealed record Event(long? Index, long? Repeat, double Value);
}

public static class CevioPitchConversion
{
    private const double TimeUnitAsTicksPerBpm = 4.8 / 120;
    private const int MinDataLength = 500;
    private const double TempValueAsNull = -1.0;

    private sealed record EventDouble(double? Index, double? Repeat, double? Value)
    {
        public CevioTrackPitchData.Event? Round() =>
            Value == null
                ? null
                : new CevioTrackPitchData.Event(
                    Index.HasValue ? (long)Math.Round(Index.Value) : null,
                    Repeat.HasValue ? (long)Math.Round(Repeat.Value) : null,
                    Value.Value);

        public static EventDouble From(CevioTrackPitchData.Event e) =>
            new(e.Index, e.Repeat, e.Value);
    }

    private readonly record struct TempoTriple(double Pos, double TickPos, double Bpm);

    public static Model.Pitch? PitchFromCevioTrack(CevioTrackPitchData data)
    {
        var convertedPoints = new List<(double Tick, double? Value)>();
        double? currentValue = null;

        var eventsNormalized = ShapeEvents(NormalizeToTick(AppendEndingPoints(data)));

        double? nextPos = null;
        foreach (var ev in eventsNormalized)
        {
            double pos = ev.Index!.Value - data.TickPrefix;
            double length = ev.Repeat!.Value;
            double? value = ev.Value?.LoggedFrequencyToKey();
            if (value != currentValue || nextPos != pos)
            {
                convertedPoints.Add((pos, value));
                currentValue = value;
            }

            nextPos = pos + length;
        }

        int lastMinusIndex = -1;
        for (int i = 0; i < convertedPoints.Count; i++)
            if (convertedPoints[i].Tick < 0 && convertedPoints[i].Value != null)
                lastMinusIndex = i;

        if (lastMinusIndex >= 0)
        {
            var lastMinusValue = convertedPoints[lastMinusIndex].Value;
            convertedPoints.RemoveRange(0, lastMinusIndex + 1);
            var first = convertedPoints.Count > 0 ? convertedPoints[0] : ((double, double?)?)null;
            if (first != null && first.Value.Item1 > 0)
                convertedPoints.Insert(0, (0.0, lastMinusValue));
        }

        var data2 = convertedPoints.Select(p => ((long)Math.Round(p.Tick), p.Value)).ToList();
        return data2.Count > 0 ? new Model.Pitch(data2, true) : null;
    }

    private static CevioTrackPitchData AppendEndingPoints(CevioTrackPitchData data)
    {
        var result = new List<CevioTrackPitchData.Event>();
        long? nextPos = null;
        foreach (var ev in data.Events)
        {
            long pos = ev.Index ?? nextPos ?? throw new InvalidOperationException();
            long length = ev.Repeat ?? 1;
            if (nextPos != null && nextPos < pos)
                result.Add(new CevioTrackPitchData.Event(nextPos, null, TempValueAsNull));
            result.Add(new CevioTrackPitchData.Event(pos, length, ev.Value));
            nextPos = pos + length;
        }

        if (nextPos != null)
            result.Add(new CevioTrackPitchData.Event(nextPos, null, TempValueAsNull));

        return data with { Events = result };
    }

    private static List<EventDouble> NormalizeToTick(CevioTrackPitchData data)
    {
        var tempos = Expand(data.Tempos.Select(t => t with { TickPosition = t.TickPosition + data.TickPrefix }).ToList());
        var events = data.Events.Select(EventDouble.From).ToList();
        var normalized = new List<EventDouble>();
        int currentTempoIndex = 0;
        double nextPos = 0.0;
        double nextTickPos = 0.0;

        foreach (var ev in events)
        {
            double pos = ev.Index ?? nextPos;
            double tickPos;
            if (ev.Index == null)
            {
                tickPos = nextTickPos;
            }
            else
            {
                while (currentTempoIndex + 1 < tempos.Count && tempos[currentTempoIndex + 1].Pos <= ev.Index.Value)
                    currentTempoIndex++;
                double ticksInTimeUnit = TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
                tickPos = tempos[currentTempoIndex].TickPos + (ev.Index.Value - tempos[currentTempoIndex].Pos) * ticksInTimeUnit;
            }

            double repeat = ev.Repeat ?? 1.0;
            double remainingRepeat = repeat;
            double repeatInTicks = 0.0;
            while (currentTempoIndex + 1 < tempos.Count && tempos[currentTempoIndex + 1].Pos < pos + repeat)
            {
                repeatInTicks += tempos[currentTempoIndex + 1].TickPos - Math.Max(tempos[currentTempoIndex].TickPos, tickPos);
                remainingRepeat -= tempos[currentTempoIndex + 1].Pos - Math.Max(tempos[currentTempoIndex].Pos, pos);
                currentTempoIndex++;
            }

            repeatInTicks += remainingRepeat * TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
            nextPos = pos + repeat;
            nextTickPos = tickPos + repeatInTicks;
            normalized.Add(new EventDouble(tickPos, repeatInTicks, ev.Value));
        }

        return normalized
            .Select(it => it with { Value = it.Value == TempValueAsNull ? null : it.Value })
            .ToList();
    }

    private static List<TempoTriple> Expand(IReadOnlyList<Tempo> tempos)
    {
        var acc = new List<TempoTriple>();
        foreach (var element in tempos)
        {
            if (acc.Count == 0)
            {
                acc.Add(new TempoTriple(0.0, 0.0, element.Bpm));
            }
            else
            {
                var last = acc[^1];
                double ticksInTimeUnit = TimeUnitAsTicksPerBpm * last.Bpm;
                double newPos = last.Pos + (element.TickPosition - last.TickPos) / ticksInTimeUnit;
                acc.Add(new TempoTriple(newPos, element.TickPosition, element.Bpm));
            }
        }

        return acc;
    }

    private static List<EventDouble> ShapeEvents(IReadOnlyList<EventDouble> eventsWithFullParams)
    {
        var acc = new List<EventDouble>();
        foreach (var ev in eventsWithFullParams.Where(e => e.Repeat!.Value > 0))
        {
            var last = acc.Count > 0 ? acc[^1] : null;
            if (last == null)
                acc.Add(ev);
            else if (last.Index == ev.Index)
                acc[^1] = ev;
            else
                acc.Add(ev);
        }

        return acc;
    }

    public static CevioTrackPitchData? GenerateForCevio(this Model.Pitch pitch, IReadOnlyList<Note> notes, IReadOnlyList<Tempo> tempos, long tickPrefix)
    {
        if (notes.Count == 0)
            return null;
        long endTick = notes[^1].TickOff;
        var data = pitch.GetAbsoluteData(notes);
        if (data == null || data.Count == 0)
            return null;

        long? nextIndex = null;
        var eventsWithFullParams = new List<EventDouble>();
        for (int i = 0; i < data.Count; i++)
        {
            var thisPoint = data[i];
            var nextPoint = i + 1 < data.Count ? data[i + 1] : ((long, double?)?)null;
            long index = thisPoint.Item1;
            if (nextIndex != null && nextIndex > index)
            {
                if (eventsWithFullParams.Count > 0)
                {
                    var lastEvent = eventsWithFullParams[^1];
                    eventsWithFullParams.RemoveAt(eventsWithFullParams.Count - 1);
                    double lastEventRepeat = index - lastEvent.Index!.Value;
                    if (lastEventRepeat >= 1)
                        eventsWithFullParams.Add(lastEvent with { Repeat = lastEventRepeat });
                }
            }

            long repeatBase = nextPoint == null ? endTick - index : nextPoint.Value.Item1 - index;
            long repeat = Math.Max(repeatBase, 1);
            nextIndex = index + repeat;
            if (thisPoint.Item2 == null)
                continue;
            double value = thisPoint.Item2.Value.KeyToLoggedFrequency();
            eventsWithFullParams.Add(new EventDouble(index, repeat, value));
        }

        var areEventsConnectedToNext = new List<bool>();
        for (int i = 0; i < eventsWithFullParams.Count; i++)
        {
            var thisEvent = eventsWithFullParams[i];
            if (i + 1 >= eventsWithFullParams.Count)
            {
                areEventsConnectedToNext.Add(false);
                continue;
            }

            var nextEvent = eventsWithFullParams[i + 1];
            double repeat = thisEvent.Repeat ?? 1.0;
            areEventsConnectedToNext.Add(thisEvent.Index!.Value + repeat >= nextEvent.Index!.Value);
        }

        var events = RemoveRedundantRepeat(RemoveRedundantIndex(MergeEventsIfPossible(
            RestoreConnection(DenormalizeFromTick(eventsWithFullParams, tempos, tickPrefix), areEventsConnectedToNext))));

        return events.Count > 0 ? new CevioTrackPitchData(events, new List<Tempo>(), tickPrefix) : null;
    }

    public static long GetLength(this CevioTrackPitchData data)
    {
        int lastIndexWithIndex = -1;
        for (int i = 0; i < data.Events.Count; i++)
            if (data.Events[i].Index != null)
                lastIndexWithIndex = i;

        long length = data.Events[lastIndexWithIndex].Index!.Value;
        for (int i = lastIndexWithIndex; i < data.Events.Count; i++)
            length += data.Events[i].Repeat ?? 1L;
        return length + MinDataLength;
    }

    private static List<CevioTrackPitchData.Event> DenormalizeFromTick(
        IReadOnlyList<EventDouble> eventsWithFullParams,
        IReadOnlyList<Tempo> temposInTicks,
        long tickPrefix)
    {
        var tempos = Expand(temposInTicks
            .Select(t => t.TickPosition != 0L ? t with { TickPosition = t.TickPosition + tickPrefix } : t)
            .ToList());
        var events = eventsWithFullParams.Select(it => it with { Index = it.Index + tickPrefix }).ToList();

        int currentTempoIndex = 0;
        var result = new List<CevioTrackPitchData.Event>();
        foreach (var ev in events)
        {
            double tickPos = ev.Index!.Value;
            while (currentTempoIndex + 1 < tempos.Count && tempos[currentTempoIndex + 1].TickPos <= tickPos)
                currentTempoIndex++;
            double ticksInTimeUnit = TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
            double pos = tempos[currentTempoIndex].Pos + (tickPos - tempos[currentTempoIndex].TickPos) / ticksInTimeUnit;
            double repeatInTicks = ev.Repeat!.Value;
            double remainingRepeatInTicks = repeatInTicks;
            double repeat = 0.0;
            while (currentTempoIndex + 1 < tempos.Count && tempos[currentTempoIndex + 1].TickPos < tickPos + repeatInTicks)
            {
                repeat += tempos[currentTempoIndex + 1].Pos - Math.Max(tempos[currentTempoIndex].Pos, pos);
                remainingRepeatInTicks -= tempos[currentTempoIndex + 1].TickPos - Math.Max(tempos[currentTempoIndex].TickPos, tickPos);
                currentTempoIndex++;
            }

            repeat += remainingRepeatInTicks / (TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm);
            var rounded = new EventDouble(pos, Math.Max(repeat, 1.0), ev.Value).Round();
            if (rounded != null)
                result.Add(rounded);
        }

        return result;
    }

    private static List<CevioTrackPitchData.Event> RestoreConnection(IReadOnlyList<CevioTrackPitchData.Event> events, IReadOnlyList<bool> connected)
    {
        var result = new List<CevioTrackPitchData.Event>();
        for (int i = 0; i < events.Count; i++)
        {
            var thisEvent = events[i];
            if (i + 1 >= events.Count)
            {
                result.Add(thisEvent);
                continue;
            }

            var nextEvent = events[i + 1];
            if (connected[i])
                result.Add(thisEvent with { Repeat = nextEvent.Index!.Value - thisEvent.Index!.Value });
            else
                result.Add(thisEvent);
        }

        return result;
    }

    private static List<CevioTrackPitchData.Event> MergeEventsIfPossible(IReadOnlyList<CevioTrackPitchData.Event> eventsWithFullParams)
    {
        var accEvents = new List<CevioTrackPitchData.Event>();
        foreach (var thisEvent in eventsWithFullParams)
        {
            if (accEvents.Count == 0)
            {
                accEvents.Add(thisEvent);
                continue;
            }

            var lastEvent = accEvents[^1];
            bool areOverlapped = lastEvent.Index!.Value + lastEvent.Repeat!.Value > thisEvent.Index!.Value;
            if (areOverlapped)
            {
                var points = new List<(long Tick, double Value)>();
                for (long t = lastEvent.Index.Value; t < lastEvent.Index.Value + lastEvent.Repeat.Value; t++)
                    points.Add((t, lastEvent.Value));
                for (long t = thisEvent.Index.Value; t < thisEvent.Index.Value + thisEvent.Repeat!.Value; t++)
                    points.Add((t, thisEvent.Value));

                var mergedPoints = points
                    .GroupBy(p => p.Tick)
                    .Select(g => (Tick: g.Key, Value: g.Average(p => p.Value)))
                    .OrderBy(p => p.Tick)
                    .ToList();

                var mergedEvents = new List<CevioTrackPitchData.Event>();
                foreach (var element in mergedPoints)
                {
                    var last = mergedEvents.Count > 0 ? mergedEvents[^1] : null;
                    if (last == null)
                        mergedEvents.Add(new CevioTrackPitchData.Event(element.Tick, 1, element.Value));
                    else if (lastEvent.Value == element.Value)
                        mergedEvents[^1] = last with { Repeat = (last.Repeat ?? 1) + 1 };
                    else
                        mergedEvents.Add(new CevioTrackPitchData.Event(element.Tick, 1, element.Value));
                }

                accEvents.RemoveAt(accEvents.Count - 1);
                accEvents.AddRange(mergedEvents);
            }
            else
            {
                bool areAdjacent = lastEvent.Index.Value + lastEvent.Repeat.Value == thisEvent.Index.Value;
                bool areValuesSame = lastEvent.Value == thisEvent.Value;
                if (areAdjacent && areValuesSame)
                    accEvents[^1] = lastEvent with { Repeat = lastEvent.Repeat.Value + thisEvent.Repeat!.Value };
                else
                    accEvents.Add(thisEvent);
            }
        }

        return accEvents;
    }

    private static List<CevioTrackPitchData.Event> RemoveRedundantIndex(IReadOnlyList<CevioTrackPitchData.Event> events)
    {
        if (events.Count == 0)
            return events.ToList();

        var result = new List<CevioTrackPitchData.Event> { events[0] };
        for (int i = 1; i < events.Count; i++)
        {
            var lastEvent = events[i - 1];
            var thisEvent = events[i];
            bool areAdjacent = lastEvent.Index!.Value + lastEvent.Repeat!.Value == thisEvent.Index;
            result.Add(areAdjacent ? thisEvent with { Index = null } : thisEvent);
        }

        return result;
    }

    private static List<CevioTrackPitchData.Event> RemoveRedundantRepeat(IReadOnlyList<CevioTrackPitchData.Event> events) =>
        events.Select(it => it.Repeat == 1L ? it with { Repeat = null } : it).ToList();
}
