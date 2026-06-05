using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ustx;

public sealed class TimeSigSegment
{
    public int BarPos { get; set; }
    public int TickPos { get; set; }
    public int BeatPerBar { get; set; }
    public int BeatUnit { get; set; }
    public int TicksPerBar { get; set; }
    public int TicksPerBeat { get; set; }
    public int BarEnd { get; set; } = int.MaxValue;
    public long TickEnd { get; set; } = long.MaxValue;
}

public sealed class TempoSegment
{
    public long TickPos { get; set; }
    public int BeatPerBar { get; set; }
    public int BeatUnit { get; set; }
    public double Bpm { get; set; } = 120;
    public double MsPos { get; set; }
    public double MsPerTick { get; set; }
    public double TicksPerMs { get; set; }
    public long TickEnd { get; set; } = long.MaxValue;
    public double MsEnd { get; set; } = double.PositiveInfinity;

    public long Ticks => TickEnd - TickPos;
}

public sealed class TimeAxis
{
    public List<TimeSigSegment> TimeSigSegments { get; } = new();
    public List<TempoSegment> TempoSegments { get; } = new();

    public void BuildSegments(USTXProject project)
    {
        TimeSigSegments.Clear();
        for (int i = 0; i < project.TimeSignatures.Count; i++)
        {
            var timesig = project.TimeSignatures[i];
            int posTick = 0;
            if (i > 0)
            {
                int lastBarPos = project.TimeSignatures[i - 1].BarPosition;
                posTick = TimeSigSegments[^1].TickPos
                    + TimeSigSegments[^1].TicksPerBar * (timesig.BarPosition - lastBarPos);
            }
            TimeSigSegments.Add(new TimeSigSegment
            {
                BarPos = timesig.BarPosition,
                TickPos = posTick,
                BeatPerBar = timesig.BeatPerBar,
                BeatUnit = timesig.BeatUnit,
                TicksPerBar = project.Resolution * 4 * timesig.BeatPerBar / timesig.BeatUnit,
                TicksPerBeat = project.Resolution * 4 / timesig.BeatUnit,
            });
        }
        for (int i = 0; i < TimeSigSegments.Count - 1; i++)
        {
            TimeSigSegments[i].BarEnd = TimeSigSegments[i + 1].BarPos;
            TimeSigSegments[i].TickEnd = TimeSigSegments[i + 1].TickPos;
        }
        if (TimeSigSegments.Count == 0)
        {
            TimeSigSegments.Add(new TimeSigSegment
            {
                BarPos = 0,
                TickPos = 0,
                BeatPerBar = project.BeatPerBar,
                BeatUnit = project.BeatUnit,
                TicksPerBar = project.Resolution * 4 * project.BeatPerBar / project.BeatUnit,
                TicksPerBeat = project.Resolution * 4 / project.BeatUnit,
            });
        }

        TempoSegments.Clear();
        foreach (var sigseg in TimeSigSegments)
        {
            TempoSegments.Add(new TempoSegment
            {
                TickPos = sigseg.TickPos,
                BeatPerBar = sigseg.BeatPerBar,
                BeatUnit = sigseg.BeatUnit,
            });
        }
        for (int i = 0; i < project.Tempos.Count; i++)
        {
            var tempo = project.Tempos[i];
            int index = -1;
            for (int j = 0; j < TempoSegments.Count; j++)
            {
                if (TempoSegments[j].TickPos >= tempo.Position)
                {
                    index = j;
                    break;
                }
            }
            if (index < 0)
            {
                TempoSegments.Add(new TempoSegment
                {
                    TickPos = tempo.Position,
                    Bpm = tempo.Bpm,
                    BeatPerBar = TempoSegments[^1].BeatPerBar,
                    BeatUnit = TempoSegments[^1].BeatUnit,
                });
            }
            else if (TempoSegments[index].TickPos == tempo.Position)
            {
                TempoSegments[index].Bpm = tempo.Bpm;
            }
            else
            {
                TempoSegments.Insert(index, new TempoSegment
                {
                    TickPos = tempo.Position,
                    Bpm = tempo.Bpm,
                    BeatPerBar = TempoSegments[index - 1].BeatPerBar,
                    BeatUnit = TempoSegments[index - 1].BeatUnit,
                });
            }
        }
        for (int i = 0; i < TempoSegments.Count - 1; i++)
        {
            if (TempoSegments[i + 1].Bpm == 0)
                TempoSegments[i + 1].Bpm = TempoSegments[i].Bpm;
            TempoSegments[i].TickEnd = TempoSegments[i + 1].TickPos;
        }
        for (int i = 0; i < TempoSegments.Count; i++)
        {
            TempoSegments[i].MsPerTick =
                60.0 * 1000.0 * TempoSegments[i].BeatPerBar
                / (TempoSegments[i].Bpm * 4 * project.Resolution);
            TempoSegments[i].TicksPerMs =
                TempoSegments[i].Bpm * 4 * project.Resolution
                / (60.0 * 1000.0 * TempoSegments[i].BeatPerBar);
            if (i > 0)
            {
                TempoSegments[i].MsPos =
                    TempoSegments[i - 1].MsPos
                    + TempoSegments[i - 1].Ticks * TempoSegments[i - 1].MsPerTick;
                TempoSegments[i - 1].MsEnd = TempoSegments[i].MsPos;
            }
        }
    }

    public double TickPosToMsPos(double tick)
    {
        foreach (var seg in TempoSegments)
        {
            if (seg.TickPos == tick || seg.TickEnd > tick)
                return seg.MsPos + seg.MsPerTick * (tick - seg.TickPos);
        }
        return 0;
    }

    public int MsPosToTickPos(double ms)
    {
        foreach (var seg in TempoSegments)
        {
            if (seg.MsPos == ms || seg.MsEnd > ms)
            {
                double tickPos = seg.TickPos + (ms - seg.MsPos) * seg.TicksPerMs;
                return (int)System.Math.Round(tickPos, System.MidpointRounding.ToEven);
            }
        }
        return 0;
    }

    public double MsBetweenTickPos(double tickPos, double tickEnd) =>
        TickPosToMsPos(tickEnd) - TickPosToMsPos(tickPos);
}
