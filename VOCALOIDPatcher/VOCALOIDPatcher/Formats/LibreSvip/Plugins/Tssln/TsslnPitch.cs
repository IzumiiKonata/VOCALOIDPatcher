using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tssln;

public readonly struct TsslnParamEvent
{
    public readonly int? Idx;
    public readonly int? Repeat;
    public readonly double Value;

    public TsslnParamEvent(int? idx, int? repeat, double value)
    {
        Idx = idx;
        Repeat = repeat;
        Value = value;
    }
}

public readonly struct TsslnParamEventFloat
{
    public readonly double? Idx;
    public readonly double? Repeat;
    public readonly double? Value;

    public TsslnParamEventFloat(double? idx, double? repeat, double? value)
    {
        Idx = idx;
        Repeat = repeat;
        Value = value;
    }

    public static TsslnParamEventFloat FromEvent(TsslnParamEvent ev) =>
        new(ev.Idx.HasValue ? ev.Idx.Value : (double?)null,
            ev.Repeat.HasValue ? ev.Repeat.Value : (double?)null,
            ev.Value);
}

public sealed class TsslnTrackPitchData
{
    public List<TsslnParamEvent> Events { get; }
    public List<SongTempo> Tempos { get; }
    public int TickPrefix { get; }
    public List<TsslnParamEvent> VibratoAmplitudeEvents { get; }
    public List<TsslnParamEvent> VibratoFrequencyEvents { get; }

    public TsslnTrackPitchData(
        List<TsslnParamEvent> events,
        List<SongTempo> tempos,
        int tickPrefix,
        List<TsslnParamEvent>? vibratoAmplitudeEvents = null,
        List<TsslnParamEvent>? vibratoFrequencyEvents = null)
    {
        Events = events;
        Tempos = tempos;
        TickPrefix = tickPrefix;
        VibratoAmplitudeEvents = vibratoAmplitudeEvents ?? new List<TsslnParamEvent>();
        VibratoFrequencyEvents = vibratoFrequencyEvents ?? new List<TsslnParamEvent>();
    }

    public int Length
    {
        get
        {
            if (Events.Count == 0)
                return TsslnConstants.MinDataLength;
            int lastHasIndex = Search.FindLastIndex(Events, e => e.Idx.HasValue);
            if (lastHasIndex < 0)
                return TsslnConstants.MinDataLength;
            int length = Events[lastHasIndex].Idx!.Value;
            for (int i = lastHasIndex; i < Events.Count; i++)
                length += Events[i].Repeat ?? 1;
            return length + TsslnConstants.MinDataLength;
        }
    }
}

public static class TsslnPitch
{
    private static double Hz2Midi(double hz) => MusicMath.Hz2Midi(hz);

    private static double Midi2Hz(double midi) => MusicMath.Midi2Hz(midi);

    public static ParamCurve? PitchFromTrack(TsslnTrackPitchData data)
    {
        var convertedPoints = new List<Point> { Point.StartPoint() };
        int currentValue = -100;

        var synchronizer = new TimeSynchronizer(data.Tempos);
        var expandedTempos = Expand(data.Tempos, data.TickPrefix);
        var vibratoAmplitudeIntervalDict = BuildParamIntervalDict(
            data.VibratoAmplitudeEvents, synchronizer, data.TickPrefix, expandedTempos);
        var vibratoValueIntervalDict = BuildWaveIntervalDict(
            data.VibratoFrequencyEvents, synchronizer, data.TickPrefix, expandedTempos);
        var eventsNormalized = ShapeEvents(
            NormalizeToTick(
                AppendEndingPoints(data.Events),
                data.Tempos,
                data.TickPrefix,
                expandedTempos));

        double? nextPos = null;
        foreach (var ev in eventsNormalized)
        {
            int pos = (int)ev.Idx!.Value - data.TickPrefix;
            double secs = synchronizer.GetActualSecsFromTicks(pos);
            double length = ev.Repeat!.Value;
            bool overflow = false;
            int value = -100;
            if (ev.Value.HasValue)
            {
                double midi = Hz2Midi(Math.Exp(ev.Value.Value));
                double scaled = Math.Round(midi * 100);
                if (double.IsInfinity(scaled) || double.IsNaN(scaled)
                    || scaled > int.MaxValue || scaled < int.MinValue)
                    overflow = true;
                else
                    value = (int)scaled;
            }
            if (!overflow)
            {
                if (value != currentValue || nextPos != pos)
                {
                    convertedPoints.Add(new Point(pos, value));
                    if (value == -100)
                        convertedPoints.Add(new Point(pos, value));
                    currentValue = value;
                }
                double secsStep = synchronizer.GetDurationSecsFromTicks(pos, pos + 5);
                for (int posX = pos; posX < (int)(pos + length); posX += 5)
                {
                    double? valueDiff = vibratoValueIntervalDict.Get(secs);
                    if (valueDiff.HasValue && valueDiff.Value != 0)
                    {
                        double diff = valueDiff.Value * vibratoAmplitudeIntervalDict.Get(secs, 1);
                        convertedPoints.Add(new Point(posX, (int)Math.Round(value + diff)));
                    }
                    else
                    {
                        break;
                    }
                    secs += secsStep;
                }
            }
            nextPos = pos + length;
        }
        convertedPoints.Add(Point.EndPoint());

        return convertedPoints.Count > 2 ? new ParamCurve { Points = convertedPoints } : null;
    }

    private static List<TsslnParamEvent> AppendEndingPoints(List<TsslnParamEvent> events)
    {
        var result = new List<TsslnParamEvent>();
        int? nextPos = null;
        foreach (var ev in events)
        {
            int? pos = ev.Idx ?? nextPos;
            if (pos == null)
                continue;
            int length = ev.Repeat ?? 1;
            if (nextPos != null && nextPos < pos)
                result.Add(new TsslnParamEvent(nextPos, null, TsslnConstants.TempValueAsNull));
            result.Add(new TsslnParamEvent(pos, length, ev.Value));
            nextPos = pos + length;
        }
        if (nextPos != null)
            result.Add(new TsslnParamEvent(nextPos, null, TsslnConstants.TempValueAsNull));
        return result;
    }

    private static List<TsslnParamEventFloat> NormalizeToTick(
        List<TsslnParamEvent> events,
        List<SongTempo> tempoList,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var tempos = expandedTempos ?? Expand(tempoList, tickPrefix);
        var normalized = new List<TsslnParamEventFloat>();
        int currentTempoIndex = 0;
        double nextPos = 0.0;
        double nextTickPos = 0.0;
        foreach (var rawEvent in events)
        {
            var ev = TsslnParamEventFloat.FromEvent(rawEvent);
            double pos = ev.Idx ?? nextPos;
            double tickPos;
            if (ev.Idx == null)
            {
                tickPos = nextTickPos;
            }
            else
            {
                while (currentTempoIndex + 1 < tempos.Count
                       && tempos[currentTempoIndex + 1].Pos <= ev.Idx.Value)
                    currentTempoIndex++;
                double ticksInTimeUnit = TsslnConstants.TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
                tickPos = tempos[currentTempoIndex].TickPos
                          + (ev.Idx.Value - tempos[currentTempoIndex].Pos) * ticksInTimeUnit;
            }
            double repeat = ev.Repeat ?? 1.0;
            double remainingRepeat = repeat;
            double repeatInTicks = 0.0;
            while (currentTempoIndex + 1 < tempos.Count
                   && tempos[currentTempoIndex + 1].Pos < pos + repeat)
            {
                repeatInTicks += tempos[currentTempoIndex + 1].TickPos
                                 - Math.Max(tempos[currentTempoIndex].TickPos, tickPos);
                remainingRepeat -= tempos[currentTempoIndex + 1].Pos
                                   - Math.Max(tempos[currentTempoIndex].Pos, pos);
                currentTempoIndex++;
            }
            repeatInTicks += remainingRepeat * TsslnConstants.TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
            nextPos = pos + repeat;
            nextTickPos = tickPos + repeatInTicks;
            normalized.Add(new TsslnParamEventFloat(tickPos, repeatInTicks, ev.Value));
        }
        return normalized.Select(tick => new TsslnParamEventFloat(
            tick.Idx!.Value + tickPrefix,
            tick.Repeat,
            tick.Value.HasValue && tick.Value.Value != TsslnConstants.TempValueAsNull ? tick.Value : null)).ToList();
    }

    private static List<TsslnParamEventFloat> ShapeEvents(List<TsslnParamEventFloat> events)
    {
        var result = new List<TsslnParamEventFloat>();
        foreach (var ev in events)
        {
            if (ev.Repeat.HasValue && ev.Repeat.Value > 0)
            {
                if (result.Count > 0 && result[^1].Idx == ev.Idx)
                    result[^1] = ev;
                else
                    result.Add(ev);
            }
        }
        return result;
    }

    private static List<(int Pos, int TickPos, double Bpm)> Expand(List<SongTempo> tempos, int tickPrefix)
    {
        var result = new List<(int Pos, int TickPos, double Bpm)>();
        for (int i = 0; i < tempos.Count; i++)
        {
            var tempo = tempos[i];
            if (i == 0)
            {
                result.Add((0, tickPrefix, tempo.Bpm));
            }
            else
            {
                var (lastPos, lastTickPos, lastBpm) = result[^1];
                double ticksInTimeUnit = TsslnConstants.TimeUnitAsTicksPerBpm * lastBpm;
                double newPos = lastPos + (tempo.Position - lastTickPos) / ticksInTimeUnit;
                result.Add(((int)newPos, tempo.Position, tempo.Bpm));
            }
        }
        return result;
    }

    private static double VibratoCurve(double value, double shift, double omega, double phase) =>
        Math.Sin(omega * (value - shift) + phase);

    public static PiecewiseIntervalDict BuildParamIntervalDict(
        List<TsslnParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var dict = new PiecewiseIntervalDict();
        foreach (var continuousPart in NormalizedContinuousParts(events, synchronizer, tickPrefix, expandedTempos))
        {
            TsslnParamEventFloat? prev = null;
            foreach (var next in continuousPart)
            {
                double nextStart = synchronizer.GetActualSecsFromTicks(next.Idx!.Value - tickPrefix);
                if (prev != null)
                {
                    double prevEnd = synchronizer.GetActualSecsFromTicks(
                        prev.Value.Idx!.Value + (prev.Value.Repeat ?? 1) - tickPrefix);
                    if (prevEnd < nextStart)
                        dict.SetConstant(prevEnd, nextStart, next.Value ?? 0);
                }
                double end = synchronizer.GetActualSecsFromTicks(
                    next.Idx!.Value + (next.Repeat ?? 1) - tickPrefix);
                dict.SetConstant(nextStart, end, next.Value ?? 0);
                prev = next;
            }
        }
        return dict;
    }

    public static PiecewiseIntervalDict BuildWaveIntervalDict(
        List<TsslnParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var dict = new PiecewiseIntervalDict();
        double omega = Math.PI * 2 * 6;
        foreach (var continuousPart in NormalizedContinuousParts(events, synchronizer, tickPrefix, expandedTempos))
        {
            double phase = 0.0;
            TsslnParamEventFloat? prev = null;
            foreach (var next in continuousPart)
            {
                double nextStart = synchronizer.GetActualSecsFromTicks(next.Idx!.Value - tickPrefix);
                double nextEnd = synchronizer.GetActualSecsFromTicks(
                    next.Idx!.Value + (next.Repeat ?? 1) - tickPrefix);
                if (prev != null)
                {
                    double prevEnd = synchronizer.GetActualSecsFromTicks(
                        prev.Value.Idx!.Value + (prev.Value.Repeat ?? 1) - tickPrefix);
                    if (prevEnd < nextStart)
                    {
                        double prevStart = synchronizer.GetActualSecsFromTicks(prev.Value.Idx!.Value - tickPrefix);
                        double capturedOmega = omega;
                        double capturedPhase = phase;
                        dict.SetConstant(prevEnd, nextStart, VibratoCurve(prevEnd, prevStart, capturedOmega, capturedPhase));
                    }
                }
                omega = Math.PI * 2 * (next.Value ?? 6);
                double segOmega = omega;
                double segPhase = phase;
                double segShift = nextStart;
                dict.Set(nextStart, nextEnd, x => VibratoCurve(x, segShift, segOmega, segPhase));
                phase += (nextEnd - nextStart) * omega;
                prev = next;
            }
        }
        return dict;
    }

    public static TsslnTrackPitchData? GenerateForTrack(
        ParamCurve pitch, List<SongTempo> tempos, int tickPrefix)
    {
        var eventsWithFullParams = new List<TsslnParamEventFloat>();
        var points = pitch.Points;
        for (int i = 0; i < points.Count; i++)
        {
            var thisPoint = points[i];
            Point? nextPoint = i + 1 < points.Count ? points[i + 1] : (Point?)null;
            int? endTick = nextPoint?.X - tickPrefix;
            int index = thisPoint.X - tickPrefix;
            int repeat = endTick.HasValue ? endTick.Value - index : 1;
            repeat = Math.Max(repeat, 1);
            double? value = thisPoint.Y != -100 ? Math.Log(Midi2Hz(thisPoint.Y / 100.0)) : (double?)null;
            if (value.HasValue && (nextPoint == null || nextPoint.Value.Y != -100))
                eventsWithFullParams.Add(new TsslnParamEventFloat(index, repeat, value));
        }
        var areEventsConnectedToNext = new List<bool>();
        for (int i = 0; i < eventsWithFullParams.Count; i++)
        {
            var thisEvent = eventsWithFullParams[i];
            if (i + 1 < eventsWithFullParams.Count)
            {
                var nextEvent = eventsWithFullParams[i + 1];
                areEventsConnectedToNext.Add(thisEvent.Idx!.Value + thisEvent.Repeat!.Value >= nextEvent.Idx!.Value);
            }
            else
            {
                areEventsConnectedToNext.Add(false);
            }
        }
        var events = DenormalizeFromTick(eventsWithFullParams, tempos, tickPrefix);
        events = RestoreConnection(events, areEventsConnectedToNext);
        events = MergeEventsIfPossible(events);
        events = RemoveRedundantIndex(events);
        events = RemoveRedundantRepeat(events);
        if (events.Count == 0)
            return null;
        return new TsslnTrackPitchData(events, new List<SongTempo>(), tickPrefix);
    }

    private static List<TsslnParamEvent> DenormalizeFromTick(
        List<TsslnParamEventFloat> eventsWithFullParams,
        List<SongTempo> temposInTicks,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var tempos = expandedTempos
                     ?? Expand(TickCounter.ShiftTempoList(temposInTicks, tickPrefix), tickPrefix);
        var shifted = eventsWithFullParams
            .Select(ev => ev.Idx == null ? ev : new TsslnParamEventFloat(ev.Idx.Value + tickPrefix, ev.Repeat, ev.Value))
            .ToList();
        var events = new List<TsslnParamEvent>();
        int currentTempoIndex = 0;
        double? tickPos = null;
        foreach (var eventDouble in shifted)
        {
            if (eventDouble.Idx != null)
                tickPos = eventDouble.Idx;
            if (tickPos == null || eventDouble.Idx == null)
                throw new InvalidOperationException("Invalid event");
            while (currentTempoIndex + 1 < tempos.Count
                   && tempos[currentTempoIndex + 1].TickPos < tickPos.Value)
                currentTempoIndex++;
            double ticksPerTimeUnit = tempos[currentTempoIndex].Bpm * TsslnConstants.TimeUnitAsTicksPerBpm;
            double pos = tempos[currentTempoIndex].Pos
                         + (eventDouble.Idx.Value - tempos[currentTempoIndex].TickPos) / ticksPerTimeUnit;
            double repeatInTicks = eventDouble.Repeat ?? 0;
            double repeat = 0.0;
            while (currentTempoIndex + 1 < tempos.Count
                   && tempos[currentTempoIndex + 1].TickPos < tickPos.Value + repeatInTicks)
            {
                repeat += tempos[currentTempoIndex + 1].Pos - Math.Max(tempos[currentTempoIndex].Pos, pos);
                repeatInTicks -= tempos[currentTempoIndex + 1].TickPos
                                 - Math.Max(tempos[currentTempoIndex].TickPos, tickPos.Value);
                currentTempoIndex++;
            }
            repeat += repeatInTicks / (TsslnConstants.TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm);
            events.Add(new TsslnParamEvent(
                (int)Math.Round(pos),
                (int)Math.Round(Math.Max(repeat, 1)),
                eventDouble.Value ?? 0));
        }
        return events;
    }

    private static List<TsslnParamEvent> RestoreConnection(
        List<TsslnParamEvent> events, List<bool> areEventsConnectedToNext)
    {
        var newEvents = new List<TsslnParamEvent>();
        for (int i = 0; i < events.Count; i++)
        {
            var prevEvent = events[i];
            TsslnParamEvent? nextEvent = i + 1 < events.Count ? events[i + 1] : (TsslnParamEvent?)null;
            bool isConnected = i < areEventsConnectedToNext.Count && areEventsConnectedToNext[i];
            if (nextEvent == null || !isConnected)
                newEvents.Add(prevEvent);
            else
                newEvents.Add(new TsslnParamEvent(prevEvent.Idx, nextEvent.Value.Idx!.Value - prevEvent.Idx!.Value, prevEvent.Value));
        }
        return newEvents;
    }

    private static List<TsslnParamEvent> MergeEventsIfPossible(List<TsslnParamEvent> events)
    {
        var newEvents = new List<TsslnParamEvent>();
        foreach (var rawEvent in events)
        {
            var ev = rawEvent;
            if (newEvents.Count == 0)
            {
                newEvents.Add(ev);
            }
            else
            {
                var lastEvent = newEvents[^1];
                int overlappedLen = lastEvent.Idx!.Value + (lastEvent.Repeat ?? 0) - ev.Idx!.Value;
                if (overlappedLen > 0)
                {
                    newEvents[^1] = new TsslnParamEvent(newEvents[^1].Idx, ev.Idx!.Value - lastEvent.Idx!.Value, newEvents[^1].Value);
                    ev = new TsslnParamEvent(overlappedLen + ev.Idx!.Value, (ev.Repeat ?? 0) - overlappedLen, ev.Value);
                    lastEvent = new TsslnParamEvent(ev.Idx, overlappedLen, ev.Value + lastEvent.Value);
                    newEvents.Add(lastEvent);
                }
                if (lastEvent.Value == ev.Value && lastEvent.Idx!.Value + (lastEvent.Repeat ?? 0) == ev.Idx!.Value)
                    newEvents[^1] = new TsslnParamEvent(newEvents[^1].Idx, (lastEvent.Repeat ?? 0) + (ev.Repeat ?? 0), newEvents[^1].Value);
                else
                    newEvents.Add(ev);
            }
        }
        return newEvents;
    }

    private static List<TsslnParamEvent> RemoveRedundantIndex(List<TsslnParamEvent> events)
    {
        var newEvents = new List<TsslnParamEvent>();
        foreach (var ev in events)
        {
            if (newEvents.Count == 0)
            {
                newEvents.Add(ev);
            }
            else
            {
                var prevEvent = newEvents[^1];
                if (prevEvent.Idx.HasValue && prevEvent.Repeat.HasValue
                    && prevEvent.Idx.Value + prevEvent.Repeat.Value == ev.Idx)
                    newEvents.Add(new TsslnParamEvent(null, ev.Repeat, ev.Value));
                else
                    newEvents.Add(ev);
            }
        }
        return newEvents;
    }

    private static List<TsslnParamEvent> RemoveRedundantRepeat(List<TsslnParamEvent> events) =>
        events.Select(ev => (ev.Repeat ?? 0) > 1 ? ev : new TsslnParamEvent(ev.Idx, null, ev.Value)).ToList();

    private static List<List<TsslnParamEventFloat>> NormalizedContinuousParts(
        List<TsslnParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var tempoList = synchronizer.TempoList.ToList();
        var parts = SplitBefore(events, e => e.Idx.HasValue);
        return parts
            .Select(part => NormalizeToTick(AppendEndingPoints(part), tempoList, tickPrefix, expandedTempos))
            .ToList();
    }

    private static List<List<TsslnParamEvent>> SplitBefore(
        List<TsslnParamEvent> events, Func<TsslnParamEvent, bool> pred)
    {
        var result = new List<List<TsslnParamEvent>>();
        var current = new List<TsslnParamEvent>();
        foreach (var ev in events)
        {
            if (pred(ev) && current.Count > 0)
            {
                result.Add(current);
                current = new List<TsslnParamEvent>();
            }
            current.Add(ev);
        }
        if (current.Count > 0)
            result.Add(current);
        return result;
    }
}
