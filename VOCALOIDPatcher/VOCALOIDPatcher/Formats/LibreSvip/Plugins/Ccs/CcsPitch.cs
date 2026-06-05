using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;

internal readonly struct CcsParamEvent
{
    public readonly int? Idx;
    public readonly int? Repeat;
    public readonly double Value;

    public CcsParamEvent(int? idx, int? repeat, double value)
    {
        Idx = idx;
        Repeat = repeat;
        Value = value;
    }
}

internal sealed class CcsTrackPitchData
{
    public List<CcsParamEvent> Events { get; set; } = new();
    public List<SongTempo> Tempos { get; set; } = new();
    public int TickPrefix { get; set; }
    public List<CcsParamEvent> VibratoAmplitudeEvents { get; set; } = new();
    public List<CcsParamEvent> VibratoFrequencyEvents { get; set; } = new();

    private const int MinDataLength = 500;

    public int Length
    {
        get
        {
            if (Events.Count == 0) return MinDataLength;
            int lastWithIndex = Search.FindLastIndex(Events, e => e.Idx != null);
            if (lastWithIndex < 0) return MinDataLength;
            var last = Events[lastWithIndex];
            int length = last.Idx!.Value + Events.Skip(lastWithIndex).Sum(e => e.Repeat ?? 1);
            return length + MinDataLength;
        }
    }
}

internal readonly struct CcsParamEventFloat
{
    public readonly double? Idx;
    public readonly double? Repeat;
    public readonly double? Value;

    public CcsParamEventFloat(double? idx, double? repeat, double? value)
    {
        Idx = idx;
        Repeat = repeat;
        Value = value;
    }
}

internal static class CcsPitchHelper
{
    private const double TimeUnitAsTicksPerBpm = 4.8 / 120.0;
    private const double TempValueAsNull = -1.0;
    private const int MinDataLength = 500;

    public static ParamCurve? PitchFromCcsTrack(CcsTrackPitchData data)
    {
        var convertedPoints = new List<Point> { Point.StartPoint() };
        int currentValue = -100;

        var synchronizer = new TimeSynchronizer(data.Tempos);
        var expandedTempos = Expand(data.Tempos, data.TickPrefix);

        var vibAmpDict = BuildParamIntervalDict(data.VibratoAmplitudeEvents, synchronizer, data.TickPrefix, expandedTempos);
        var vibFrqDict = BuildWaveIntervalDict(data.VibratoFrequencyEvents, synchronizer, data.TickPrefix, expandedTempos);

        var eventsNormalized = ShapeEvents(
            NormalizeToTick(
                AppendEndingPoints(data.Events),
                data.Tempos,
                data.TickPrefix,
                expandedTempos
            )
        );

        int? nextPos = null;
        foreach (var ev in eventsNormalized)
        {
            int pos = (int)ev.Idx!.Value - data.TickPrefix;
            double secs = synchronizer.GetActualSecsFromTicks(pos);
            double repeatLen = ev.Repeat ?? 1.0;

            int value;
            try
            {
                value = ev.Value != null
                    ? (int)Math.Round(MusicMath.Hz2Midi(Math.Exp(ev.Value.Value)) * 100)
                    : -100;
            }
            catch (OverflowException)
            {
                value = -100;
            }

            if (value != currentValue || nextPos != pos)
            {
                convertedPoints.Add(new Point(pos, value));
                if (value == -100)
                    convertedPoints.Add(new Point(pos, value));
                currentValue = value;
            }

            double secsStep = synchronizer.GetDurationSecsFromTicks(pos, pos + 5);
            for (int posX = pos; posX < (int)(pos + repeatLen); posX += 5)
            {
                double? vibFrqVal = vibFrqDict.Get(secs);
                if (vibFrqVal.HasValue)
                {
                    double ampVal = vibAmpDict.Get(secs, 1.0);
                    convertedPoints.Add(new Point(posX, (int)Math.Round(value + vibFrqVal.Value * ampVal)));
                }
                else
                {
                    break;
                }
                secs += secsStep;
            }

            nextPos = pos + (int)repeatLen;
        }

        convertedPoints.Add(Point.EndPoint());

        return convertedPoints.Count > 2
            ? new ParamCurve { Points = convertedPoints }
            : null;
    }

    private static List<CcsParamEvent> AppendEndingPoints(List<CcsParamEvent> events)
    {
        var result = new List<CcsParamEvent>();
        int? nextPos = null;
        foreach (var ev in events)
        {
            int? pos = ev.Idx ?? nextPos;
            if (pos == null) continue;
            int length = ev.Repeat ?? 1;
            if (nextPos != null && nextPos.Value < pos.Value)
                result.Add(new CcsParamEvent(nextPos.Value, null, TempValueAsNull));
            result.Add(new CcsParamEvent(pos.Value, length, ev.Value));
            nextPos = pos.Value + length;
        }
        if (nextPos != null)
            result.Add(new CcsParamEvent(nextPos.Value, null, TempValueAsNull));
        return result;
    }

    private static List<CcsParamEventFloat> NormalizeToTick(
        List<CcsParamEvent> events,
        List<SongTempo> tempoList,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)>? expandedTempos = null)
    {
        var tempos = expandedTempos ?? Expand(tempoList, tickPrefix);
        var result = new List<CcsParamEventFloat>();
        int currentTempoIndex = 0;
        double nextPos = 0.0;
        double nextTickPos = 0.0;

        foreach (var ev in events)
        {
            double pos = ev.Idx.HasValue ? (double)ev.Idx.Value : nextPos;
            double tickPos;
            if (!ev.Idx.HasValue)
            {
                tickPos = nextTickPos;
            }
            else
            {
                while (currentTempoIndex + 1 < tempos.Count &&
                       tempos[currentTempoIndex + 1].Pos <= (double)ev.Idx.Value)
                    currentTempoIndex++;
                double ticksInTimeUnit = TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;
                tickPos = tempos[currentTempoIndex].TickPos
                    + ((double)ev.Idx.Value - tempos[currentTempoIndex].Pos) * ticksInTimeUnit;
            }

            double repeat = ev.Repeat.HasValue ? (double)ev.Repeat.Value : 1.0;
            double remainingRepeat = repeat;
            double repeatInTicks = 0.0;

            while (currentTempoIndex + 1 < tempos.Count &&
                   tempos[currentTempoIndex + 1].Pos < pos + repeat)
            {
                repeatInTicks += tempos[currentTempoIndex + 1].TickPos
                    - Math.Max(tempos[currentTempoIndex].TickPos, tickPos);
                remainingRepeat -= tempos[currentTempoIndex + 1].Pos
                    - Math.Max(tempos[currentTempoIndex].Pos, pos);
                currentTempoIndex++;
            }
            repeatInTicks += remainingRepeat * TimeUnitAsTicksPerBpm * tempos[currentTempoIndex].Bpm;

            nextPos = pos + repeat;
            nextTickPos = tickPos + repeatInTicks;
            result.Add(new CcsParamEventFloat(tickPos, repeatInTicks, ev.Value));
        }

        return result.Select(tick => new CcsParamEventFloat(
            tick.Idx + tickPrefix,
            tick.Repeat,
            tick.Value == TempValueAsNull ? null : tick.Value
        )).ToList();
    }

    private static List<CcsParamEventFloat> ShapeEvents(List<CcsParamEventFloat> events)
    {
        var result = new List<CcsParamEventFloat>();
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
        var result = new List<(int, int, double)>();
        for (int i = 0; i < tempos.Count; i++)
        {
            if (i == 0)
            {
                result.Add((0, tickPrefix, tempos[i].Bpm));
            }
            else
            {
                var (lastPos, lastTickPos, lastBpm) = result[^1];
                double ticksInTimeUnit = TimeUnitAsTicksPerBpm * lastBpm;
                int newPos = (int)(lastPos + (tempos[i].Position - lastTickPos) / ticksInTimeUnit);
                result.Add((newPos, tempos[i].Position, tempos[i].Bpm));
            }
        }
        return result;
    }

    public static CcsTrackPitchData? GenerateForCcs(ParamCurve pitch, List<SongTempo> tempos, int tickPrefix)
    {
        var eventsWithFullParams = new List<CcsParamEventFloat>();
        var points = pitch.Points;
        for (int i = 0; i < points.Count; i++)
        {
            var thisPoint = points[i];
            var nextPoint = i + 1 < points.Count ? (Point?)points[i + 1] : null;
            int? endTick = nextPoint.HasValue ? nextPoint.Value.X - tickPrefix : (int?)null;
            int index = thisPoint.X - tickPrefix;
            double repeat = endTick.HasValue ? Math.Max(endTick.Value - index, 1) : 1.0;
            double? value = thisPoint.Y != -100
                ? Math.Log(MusicMath.Midi2Hz(thisPoint.Y / 100.0))
                : (double?)null;
            if (value != null && (nextPoint == null || nextPoint.Value.Y != -100))
                eventsWithFullParams.Add(new CcsParamEventFloat((double)index, repeat, value));
        }

        var areConnectedToNext = new List<bool>();
        for (int i = 0; i < eventsWithFullParams.Count; i++)
        {
            if (i + 1 < eventsWithFullParams.Count)
            {
                var thisEv = eventsWithFullParams[i];
                var nextEv = eventsWithFullParams[i + 1];
                areConnectedToNext.Add(thisEv.Idx + thisEv.Repeat >= nextEv.Idx);
            }
            else
            {
                areConnectedToNext.Add(false);
            }
        }

        var shiftedTempos = TickCounter.ShiftTempoList(tempos, tickPrefix);
        var expandedTempos = Expand(shiftedTempos, tickPrefix);

        var events = DenormalizeFromTick(eventsWithFullParams, tempos, tickPrefix, expandedTempos);
        events = RestoreConnection(events, areConnectedToNext);
        events = MergeEventsIfPossible(events);
        events = RemoveRedundantIndex(events);
        events = RemoveRedundantRepeat(events);

        if (events.Count == 0) return null;
        return new CcsTrackPitchData
        {
            Events = events,
            Tempos = new List<SongTempo>(),
            TickPrefix = tickPrefix,
        };
    }

    private static List<CcsParamEvent> DenormalizeFromTick(
        List<CcsParamEventFloat> eventsWithFullParams,
        List<SongTempo> temposInTicks,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)> expandedTempos)
    {
        var adjusted = eventsWithFullParams
            .Select(e => e.Idx.HasValue
                ? new CcsParamEventFloat(e.Idx + tickPrefix, e.Repeat, e.Value)
                : e)
            .ToList();

        var events = new List<CcsParamEvent>();
        int currentTempoIndex = 0;
        double? tickPosTracked = null;

        foreach (var evDouble in adjusted)
        {
            if (evDouble.Idx.HasValue)
                tickPosTracked = evDouble.Idx.Value;
            if (tickPosTracked == null || !evDouble.Idx.HasValue)
                continue;

            double tickPos = tickPosTracked.Value;
            while (currentTempoIndex + 1 < expandedTempos.Count &&
                   expandedTempos[currentTempoIndex + 1].TickPos < tickPos)
                currentTempoIndex++;

            var (tPos, tTickPos, tBpm) = expandedTempos[currentTempoIndex];
            double ticksPerTimeUnit = tBpm * TimeUnitAsTicksPerBpm;
            double pos = tPos + (evDouble.Idx!.Value - tTickPos) / ticksPerTimeUnit;

            double repeatInTicks = evDouble.Repeat ?? 1.0;
            double repeat = 0.0;
            while (currentTempoIndex + 1 < expandedTempos.Count &&
                   expandedTempos[currentTempoIndex + 1].TickPos < tickPos + repeatInTicks)
            {
                repeat += expandedTempos[currentTempoIndex + 1].Pos
                    - Math.Max(expandedTempos[currentTempoIndex].Pos, pos);
                repeatInTicks -= expandedTempos[currentTempoIndex + 1].TickPos
                    - Math.Max(expandedTempos[currentTempoIndex].TickPos, tickPos);
                currentTempoIndex++;
            }
            repeat += repeatInTicks / (TimeUnitAsTicksPerBpm * expandedTempos[currentTempoIndex].Bpm);

            events.Add(new CcsParamEvent(
                (int)Math.Round(pos),
                (int)Math.Round(Math.Max(repeat, 1.0)),
                evDouble.Value ?? 0.0
            ));
        }
        return events;
    }

    private static List<CcsParamEvent> RestoreConnection(
        List<CcsParamEvent> events,
        List<bool> areConnectedToNext)
    {
        var newEvents = new List<CcsParamEvent>();
        for (int i = 0; i < events.Count; i++)
        {
            var prevEv = events[i];
            bool isConnected = areConnectedToNext[i];
            if (!isConnected || i + 1 >= events.Count)
            {
                newEvents.Add(prevEv);
            }
            else
            {
                var nextEv = events[i + 1];
                newEvents.Add(new CcsParamEvent(prevEv.Idx, nextEv.Idx - prevEv.Idx, prevEv.Value));
            }
        }
        return newEvents;
    }

    private static List<CcsParamEvent> MergeEventsIfPossible(List<CcsParamEvent> events)
    {
        var newEvents = new List<CcsParamEvent>();
        foreach (var ev in events)
        {
            if (newEvents.Count == 0)
            {
                newEvents.Add(ev);
                continue;
            }
            var last = newEvents[^1];
            int overlappedLen = (last.Idx ?? 0) + (last.Repeat ?? 1) - (ev.Idx ?? 0);
            if (overlappedLen > 0)
            {
                newEvents[^1] = new CcsParamEvent(last.Idx, (ev.Idx ?? 0) - (last.Idx ?? 0), last.Value);
                var mergedEv = new CcsParamEvent(
                    (ev.Idx ?? 0) + overlappedLen,
                    (ev.Repeat ?? 1) - overlappedLen,
                    ev.Value
                );
                var blendEv = new CcsParamEvent(ev.Idx, overlappedLen, ev.Value + last.Value);
                newEvents.Add(blendEv);
                last = blendEv;
                if (last.Value == ev.Value && (last.Idx ?? 0) + (last.Repeat ?? 1) == (mergedEv.Idx ?? 0))
                    newEvents[^1] = new CcsParamEvent(last.Idx, (last.Repeat ?? 1) + (mergedEv.Repeat ?? 1), last.Value);
                else
                    newEvents.Add(mergedEv);
            }
            else if (last.Value == ev.Value && (last.Idx ?? 0) + (last.Repeat ?? 1) == (ev.Idx ?? 0))
            {
                newEvents[^1] = new CcsParamEvent(last.Idx, (last.Repeat ?? 1) + (ev.Repeat ?? 1), last.Value);
            }
            else
            {
                newEvents.Add(ev);
            }
        }
        return newEvents;
    }

    private static List<CcsParamEvent> RemoveRedundantIndex(List<CcsParamEvent> events)
    {
        var newEvents = new List<CcsParamEvent>();
        foreach (var ev in events)
        {
            if (newEvents.Count == 0)
            {
                newEvents.Add(ev);
                continue;
            }
            var prev = newEvents[^1];
            if (prev.Idx != null && prev.Repeat != null &&
                prev.Idx.Value + prev.Repeat.Value == ev.Idx)
                newEvents.Add(new CcsParamEvent(null, ev.Repeat, ev.Value));
            else
                newEvents.Add(ev);
        }
        return newEvents;
    }

    private static List<CcsParamEvent> RemoveRedundantRepeat(List<CcsParamEvent> events) =>
        events.Select(e => e.Repeat > 1
            ? e
            : new CcsParamEvent(e.Idx, null, e.Value)).ToList();

    private static PiecewiseIntervalDict BuildParamIntervalDict(
        List<CcsParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)> expandedTempos)
    {
        var dict = new PiecewiseIntervalDict();
        foreach (var continuousPart in NormalizedContinuousParts(events, synchronizer, tickPrefix, expandedTempos))
        {
            CcsParamEventFloat? prev = null;
            foreach (var next in continuousPart)
            {
                double nextStart = synchronizer.GetActualSecsFromTicks(next.Idx!.Value - tickPrefix);
                if (prev.HasValue)
                {
                    double prevEnd = synchronizer.GetActualSecsFromTicks(
                        prev.Value.Idx!.Value + (prev.Value.Repeat ?? 1) - tickPrefix);
                    if (prevEnd < nextStart)
                        dict.SetConstant(prevEnd, nextStart, next.Value ?? 0.0);
                }
                double thisEnd = synchronizer.GetActualSecsFromTicks(
                    next.Idx!.Value + (next.Repeat ?? 1) - tickPrefix);
                dict.SetConstant(nextStart, thisEnd, next.Value ?? 0.0);
                prev = next;
            }
        }
        return dict;
    }

    private static PiecewiseIntervalDict BuildWaveIntervalDict(
        List<CcsParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)> expandedTempos)
    {
        var dict = new PiecewiseIntervalDict();
        double omega = Math.Tau * 6;
        foreach (var continuousPart in NormalizedContinuousParts(events, synchronizer, tickPrefix, expandedTempos))
        {
            double phase = 0.0;
            CcsParamEventFloat? prev = null;
            foreach (var next in continuousPart)
            {
                double nextStart = synchronizer.GetActualSecsFromTicks(next.Idx!.Value - tickPrefix);
                double nextEnd = synchronizer.GetActualSecsFromTicks(
                    next.Idx!.Value + (next.Repeat ?? 1) - tickPrefix);
                if (prev.HasValue)
                {
                    double prevEnd = synchronizer.GetActualSecsFromTicks(
                        prev.Value.Idx!.Value + (prev.Value.Repeat ?? 1) - tickPrefix);
                    if (prevEnd < nextStart)
                    {
                        double prevStart = synchronizer.GetActualSecsFromTicks(prev.Value.Idx!.Value - tickPrefix);
                        double capturedOmega = omega;
                        double capturedPhase = phase;
                        double capturedPrevStart = prevStart;
                        dict.Set(prevEnd, nextStart, s => Math.Sin(capturedOmega * (s - capturedPrevStart) + capturedPhase));
                    }
                }
                omega = Math.Tau * (next.Value ?? 6.0);
                double capturedNextStart = nextStart;
                double capturedNextOmega = omega;
                double capturedNextPhase = phase;
                dict.Set(nextStart, nextEnd, s => Math.Sin(capturedNextOmega * (s - capturedNextStart) + capturedNextPhase));
                phase += (nextEnd - nextStart) * omega;
                prev = next;
            }
        }
        return dict;
    }

    private static List<List<CcsParamEventFloat>> NormalizedContinuousParts(
        List<CcsParamEvent> events,
        TimeSynchronizer synchronizer,
        int tickPrefix,
        List<(int Pos, int TickPos, double Bpm)> expandedTempos)
    {
        var result = new List<List<CcsParamEventFloat>>();
        var currentPart = new List<CcsParamEvent>();
        foreach (var ev in events)
        {
            if (ev.Idx.HasValue && currentPart.Count > 0)
            {
                result.Add(NormalizeToTick(AppendEndingPoints(currentPart), synchronizer.TempoList.ToList(), tickPrefix, expandedTempos));
                currentPart = new List<CcsParamEvent>();
            }
            currentPart.Add(ev);
        }
        if (currentPart.Count > 0)
            result.Add(NormalizeToTick(AppendEndingPoints(currentPart), synchronizer.TempoList.ToList(), tickPrefix, expandedTempos));
        return result;
    }
}
