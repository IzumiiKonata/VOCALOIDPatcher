using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public readonly record struct ControllerEvent(int Pos, int Value);

public sealed class ControllerCurve
{
    public string Name { get; }
    public List<ControllerEvent> Events { get; }
    public int DefaultValue { get; }
    public int MinValue { get; }
    public int MaxValue { get; }

    public ControllerCurve(string name, List<ControllerEvent> events, int defaultValue = 0, int minValue = -127, int maxValue = 127)
    {
        Name = name;
        Events = events.OrderBy(e => e.Pos).ToList();
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public int GetValueAt(int pos)
    {
        int current = DefaultValue;
        foreach (var e in Events)
        {
            if (e.Pos <= pos)
                current = e.Value;
            else
                break;
        }
        return current;
    }

    public bool IsEmpty => Events.Count == 0;
}

public sealed class PitchBendData
{
    public ControllerCurve Pit { get; }
    public ControllerCurve Pbs { get; }

    public PitchBendData(ControllerCurve pit, ControllerCurve pbs)
    {
        Pit = pit;
        Pbs = pbs;
    }

    public bool IsEmpty => Pit.IsEmpty && Pbs.IsEmpty;
}

public sealed class VocaloidPitchHandler
{
    private const int PitchMaxValue = 8191;
    private const int DefaultPbs = 2;
    private const int MaxPbs = 24;

    private readonly TimeSynchronizer _synchronizer;
    private readonly List<Note> _noteList;
    private readonly List<TimeSignature> _timeSignatureList;
    private readonly int _firstBarLength;

    public VocaloidPitchHandler(TimeSynchronizer synchronizer, List<Note> noteList, List<TimeSignature> timeSignatureList, int firstBarLength)
    {
        _synchronizer = synchronizer;
        _noteList = noteList;
        _timeSignatureList = timeSignatureList;
        _firstBarLength = firstBarLength;
    }

    private static List<(int Pos, double Pitch)> CombinePitPbs(ControllerCurve pit, ControllerCurve pbs)
    {
        var positions = pit.Events.Select(e => e.Pos).Concat(pbs.Events.Select(e => e.Pos)).Distinct().OrderBy(p => p).ToList();
        var result = new List<(int, double)>();
        foreach (int pos in positions)
        {
            int currentPbs = Math.Max(1, Math.Min(pbs.GetValueAt(pos), MaxPbs));
            int pitValue = pit.GetValueAt(pos);
            int denominator = pitValue > 0 ? PitchMaxValue : PitchMaxValue + 1;
            double pitchCents = (double)pitValue / denominator * currentPbs * 100;
            result.Add((pos, pitchCents));
        }
        return result;
    }

    public ParamCurve? ToAbsolutePitch(List<PitchBendData> pitchDataList, List<int>? partOffsets = null)
    {
        if (pitchDataList.Count == 0)
            return null;
        partOffsets ??= Enumerable.Repeat(0, pitchDataList.Count).ToList();

        var allPoints = new List<(int Pos, double Pitch)>();
        for (int i = 0; i < pitchDataList.Count; i++)
        {
            if (pitchDataList[i].IsEmpty)
                continue;
            foreach (var (pos, pitch) in CombinePitPbs(pitchDataList[i].Pit, pitchDataList[i].Pbs))
                allPoints.Add((pos + partOffsets[i], pitch));
        }
        if (allPoints.Count == 0 || _noteList.Count == 0)
            return null;

        allPoints.Sort((a, b) => a.Pos.CompareTo(b.Pos));
        var unique = new Dictionary<int, double>();
        foreach (var (pos, pitch) in allPoints)
            unique[pos] = pitch;

        var pointsData = unique.OrderBy(kv => kv.Key)
            .Select(kv => new Point(kv.Key, (int)Math.Round(kv.Value))).ToList();
        if (pointsData.Count == 0)
            return null;

        var simulator = new PitchSimulator(_synchronizer, PortamentoPitch.VocaloidPortamento(), _noteList, _timeSignatureList);
        return new RelativePitchCurve(_firstBarLength).ToAbsolute(pointsData, simulator);
    }

    public PitchBendData FromAbsolutePitch(ParamCurve pitch)
    {
        var empty = new PitchBendData(
            new ControllerCurve("pitch_bend", new List<ControllerEvent>(), 0, -8192, 8191),
            new ControllerCurve("pitch_bend_sens", new List<ControllerEvent>(), DefaultPbs, 1, 24));
        if (pitch.Points.Count == 0)
            return empty;

        var simulator = new PitchSimulator(_synchronizer, PortamentoPitch.VocaloidPortamento(), _noteList, _timeSignatureList);
        var relative = new RelativePitchCurve(_firstBarLength).FromAbsolute(pitch.Points, simulator);
        if (relative.Count == 0)
            return empty;

        var pitEvents = new List<ControllerEvent>();
        var pbsEvents = new List<ControllerEvent>();
        foreach (var section in SplitIntoSections(relative))
        {
            if (section.Count == 0)
                continue;
            double maxAbs = section.Max(p => Math.Abs(p.Y)) / 100.0;
            int optimalPbs = Math.Min((int)Math.Ceiling(maxAbs), MaxPbs);
            optimalPbs = Math.Max(optimalPbs, DefaultPbs);
            if (optimalPbs != DefaultPbs)
            {
                pbsEvents.Add(new ControllerEvent(section[0].X, optimalPbs));
                pbsEvents.Add(new ControllerEvent(section[^1].X + Constants.MinBreakLengthBetweenPitchSections / 2, DefaultPbs));
            }
            foreach (var point in section)
            {
                int pitValue = (int)MusicMath.Clamp(point.Y / 100.0 * PitchMaxValue / optimalPbs, -PitchMaxValue - 1, PitchMaxValue);
                pitEvents.Add(new ControllerEvent(point.X, pitValue));
            }
        }
        return new PitchBendData(
            new ControllerCurve("pitch_bend", pitEvents, 0, -8192, 8191),
            new ControllerCurve("pitch_bend_sens", pbsEvents, DefaultPbs, 1, 24));
    }

    private static List<List<Point>> SplitIntoSections(List<Point> points)
    {
        var sections = new List<List<Point>> { new() };
        int currentPos = 0;
        foreach (var point in points)
        {
            if (sections[^1].Count == 0 || point.X - currentPos < Constants.MinBreakLengthBetweenPitchSections)
                sections[^1].Add(point);
            else
                sections.Add(new List<Point> { point });
            currentPos = point.X;
        }
        return sections.Where(s => s.Count > 0).ToList();
    }
}
