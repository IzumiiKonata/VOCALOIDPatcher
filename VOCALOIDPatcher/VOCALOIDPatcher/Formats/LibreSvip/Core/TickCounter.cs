using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public static class TickCounter
{
    public static List<SongTempo> SkipTempoList(List<SongTempo> tempoList, int skipTicks)
    {
        var result = tempoList
            .Where(t => t.Position >= skipTicks)
            .Select(t => new SongTempo(t.Position - skipTicks, t.Bpm))
            .ToList();
        if (result.Count == 0 || result[0].Position > 0)
        {
            int i = Search.FindLastIndex(tempoList, t => t.Position <= skipTicks);
            SongTempo tempo = i == -1 && tempoList.Count == 0
                ? new SongTempo()
                : new SongTempo(0, tempoList[i].Bpm);
            result.Insert(0, tempo);
        }
        return result;
    }

    public static List<TimeSignature> SkipBeatList(List<TimeSignature> beatList, int skipBars)
    {
        var result = beatList
            .Where(b => b.BarIndex >= skipBars)
            .Select(b => new TimeSignature(b.BarIndex - skipBars, b.Numerator, b.Denominator))
            .ToList();
        if (result.Count == 0 || result[0].BarIndex > 0)
        {
            int i = Search.FindLastIndex(beatList, b => b.BarIndex <= skipBars);
            TimeSignature beat = i == -1 && beatList.Count == 0
                ? new TimeSignature()
                : new TimeSignature(0, beatList[i].Numerator, beatList[i].Denominator);
            result.Insert(0, beat);
        }
        return result;
    }

    public static List<SongTempo> ShiftTempoList(List<SongTempo> tempoList, int shiftTicks)
    {
        var result = tempoList.Take(1).Select(t => t.Clone()).ToList();
        result.AddRange(tempoList.Skip(1).Select(t => new SongTempo(t.Position + shiftTicks, t.Bpm)));
        return result;
    }

    public static List<TimeSignature> ShiftBeatList(List<TimeSignature> beatList, int shiftBars)
    {
        var result = beatList.Take(1).Select(b => b.Clone()).ToList();
        result.AddRange(beatList.Skip(1).Select(b => new TimeSignature(b.BarIndex + shiftBars, b.Numerator, b.Denominator)));
        return result;
    }

    public static int CalcBarIndex(double tick, double baseTick, TimeSignature beat) =>
        beat.BarIndex + (int)Math.Ceiling((tick - baseTick) / beat.BarLength());

    public static int FindBarIndex(List<TimeSignature> beatList, int ticks)
    {
        if (beatList.Count == 0)
            throw new ArgumentException("beat_list must not be empty");
        double tick = 0.0;
        for (int i = 0; i < beatList.Count - 1; i++)
        {
            var beat = beatList[i];
            var nextBeat = beatList[i + 1];
            double nextTick = tick + beat.BarLength() * (nextBeat.BarIndex - beat.BarIndex);
            if (ticks < nextTick)
                return CalcBarIndex(ticks, tick, beat);
            tick = nextTick;
        }
        return CalcBarIndex(ticks, tick, beatList[^1]);
    }
}
