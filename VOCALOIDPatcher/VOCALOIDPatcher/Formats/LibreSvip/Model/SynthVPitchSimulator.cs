using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public readonly record struct NoteStruct(
    int Key,
    double Start,
    double End,
    double PortamentoOffset,
    double PortamentoLeft,
    double PortamentoRight,
    double DepthLeft,
    double DepthRight,
    double VibratoStart,
    double VibratoLeft,
    double VibratoRight,
    double VibratoDepth,
    double VibratoFrequency,
    double VibratoPhase);

public readonly record struct PitchControlPoint(int Offset, double Value);

public sealed class PitchControl
{
    public int Pos { get; set; }
    public double Pitch { get; set; }
    public string Type { get; set; } = "curve";
    public List<PitchControlPoint> Points { get; set; } = new();
}

public sealed class PitchControlCurve
{
    public List<int> Ticks { get; }
    public List<double> Values { get; }

    public PitchControlCurve(List<int> ticks, List<double> values)
    {
        Ticks = ticks;
        Values = values;
    }

    public double? ValueAtTicks(int ticks)
    {
        int index = Search.BisectRight(Ticks, ticks) - 1;
        if (index < 0)
            return null;
        int currentTick = Ticks[index];
        double currentValue = Values[index];
        if (currentTick == ticks)
            return currentValue;
        int nextIndex = index + 1;
        if (nextIndex >= Ticks.Count)
            return null;
        int nextTick = Ticks[nextIndex];
        if (ticks > nextTick)
            return null;
        double nextValue = Values[nextIndex];
        return currentValue + (nextValue - currentValue) * (ticks - currentTick) / (double)(nextTick - currentTick);
    }
}

public sealed class PitchControlIntervalMap
{
    private readonly List<PitchControlCurve> _curves = new();
    private readonly List<int> _curveStarts = new();
    private readonly List<int> _curveEnds = new();
    private int? _lastCurveIndex;

    public void AppendCurve(PitchControlCurve curve)
    {
        if (curve.Ticks.Count == 0)
            return;
        _curves.Add(curve);
        _curveStarts.Add(curve.Ticks[0]);
        _curveEnds.Add(curve.Ticks[^1]);
        _lastCurveIndex = null;
    }

    public double? Get(int ticks)
    {
        if (_lastCurveIndex is { } cachedIndex)
        {
            var cachedCurve = _curves[cachedIndex];
            if (cachedCurve.Ticks[0] <= ticks && ticks <= cachedCurve.Ticks[^1]
                && cachedCurve.ValueAtTicks(ticks) is { } cachedValue)
                return cachedValue;
        }
        int right = Search.BisectRight(_curveStarts, ticks);
        int left = Search.BisectLeft(_curveEnds, ticks);
        if (left >= right)
        {
            _lastCurveIndex = null;
            return null;
        }
        for (int index = right - 1; index >= left; index--)
        {
            if (_curves[index].ValueAtTicks(ticks) is { } value)
            {
                _lastCurveIndex = index;
                return value;
            }
        }
        _lastCurveIndex = null;
        return null;
    }
}

internal sealed class SigmoidNode
{
    public double Start { get; }
    public double End { get; }
    public double Center { get; }
    public int KeyLeft { get; }
    public int KeyRight { get; }
    private readonly double _k;
    private readonly double _base;
    private readonly double _h;

    public SigmoidNode(double center, double halfLeft, double halfRight, int keyLeft, int keyRight)
    {
        Center = center;
        Start = center - halfLeft;
        End = center + halfRight;
        KeyLeft = keyLeft;
        KeyRight = keyRight;
        _h = (keyRight - keyLeft) * 100.0;
        _base = keyLeft * 100.0;
        _k = 3.0 / (halfLeft + halfRight);
    }

    public double ValueAtSecs(double secs)
    {
        double arg = _k * (secs - Center);
        if (arg >= 10.0)
            return _base + _h;
        if (arg <= -10.0)
            return _base;
        return _base + _h / (1.0 + Math.Exp(-arg));
    }

    public double SlopeAtSecs(double secs)
    {
        double arg = _k * (secs - Center);
        if (arg >= 10.0 || arg <= -10.0)
            return 0.0;
        double e = Math.Exp(-arg);
        return _h * _k * e / Math.Pow(1.0 + e, 2);
    }
}

internal sealed class BaseLayerGenerator
{
    private const double MaxBreak = 0.01;
    private const double PortamentoHalfwidthRatio = 0.3;
    private const double HalfwidthFloorSecs = 1.0 / 256;

    public List<NoteStruct> NoteList { get; }
    private readonly List<SigmoidNode> _nodes = new();
    private readonly List<double> _starts = new();
    private readonly List<double> _ends = new();
    private readonly List<double> _noteStarts = new();
    private readonly List<double> _noteEnds = new();
    private int? _lastNoteIndex;

    public BaseLayerGenerator(List<NoteStruct> noteList)
    {
        NoteList = noteList;
        if (noteList.Count == 0)
            return;
        for (int i = 0; i < noteList.Count - 1; i++)
        {
            var current = noteList[i];
            var next = noteList[i + 1];
            if (current.Key == next.Key)
                continue;
            if (next.Start - current.End > MaxBreak)
                continue;
            double halfLeft = Math.Max(next.PortamentoLeft * PortamentoHalfwidthRatio, HalfwidthFloorSecs);
            double halfRight = Math.Max(current.PortamentoRight * PortamentoHalfwidthRatio, HalfwidthFloorSecs);
            double mid = (current.End + next.Start) / 2 + next.PortamentoOffset;
            _nodes.Add(new SigmoidNode(mid, halfLeft, halfRight, current.Key, next.Key));
        }
        _starts.AddRange(_nodes.Select(n => n.Start));
        _ends.AddRange(_nodes.Select(n => n.End));
        _noteStarts.AddRange(noteList.Select(n => n.Start));
        _noteEnds.AddRange(noteList.Select(n => n.End));
    }

    public double PitchAtSecs(double secs)
    {
        if (NoteList.Count == 0)
            return 0.0;
        int leftIndex = Search.BisectRight(_ends, secs);
        int rightIndex = Search.BisectRight(_starts, secs);
        int queryCount = rightIndex - leftIndex;
        if (queryCount <= 0)
        {
            int? noteIndex = FindNoteIndex(secs);
            if (noteIndex is { } ni)
                return NoteList[ni].Key * 100.0;
            return NoteList[NearestNoteIndex(secs)].Key * 100.0;
        }
        if (queryCount == 1)
            return _nodes[leftIndex].ValueAtSecs(secs);
        if (queryCount <= 3)
        {
            var first = _nodes[leftIndex];
            var last = _nodes[rightIndex - 1];
            double width = first.End - last.Start;
            double bottom = first.ValueAtSecs(last.Start);
            double top = last.ValueAtSecs(first.End);
            double diff1 = first.SlopeAtSecs(last.Start);
            double diff2 = last.SlopeAtSecs(first.End);
            double x = secs - last.Start;
            double a = (2 * (bottom - top) + (diff1 + diff2) * width) / Math.Pow(width, 3);
            double b = (3 * (top - bottom) - 2 * diff1 * width - diff2 * width) / Math.Pow(width, 2);
            return a * Math.Pow(x, 3) + b * Math.Pow(x, 2) + diff1 * x + bottom;
        }
        throw new ParamsException("More than three sigmoid nodes overlapped");
    }

    private int? FindNoteIndex(double secs)
    {
        if (_lastNoteIndex is { } lastIndex)
        {
            var note = NoteList[lastIndex];
            if (note.Start <= secs && secs < note.End)
                return lastIndex;
            int nextIndex = lastIndex + 1;
            if (nextIndex < NoteList.Count)
            {
                var nextNote = NoteList[nextIndex];
                if (nextNote.Start <= secs && secs < nextNote.End)
                {
                    _lastNoteIndex = nextIndex;
                    return nextIndex;
                }
            }
            int prevIndex = lastIndex - 1;
            if (prevIndex >= 0)
            {
                var prevNote = NoteList[prevIndex];
                if (prevNote.Start <= secs && secs < prevNote.End)
                {
                    _lastNoteIndex = prevIndex;
                    return prevIndex;
                }
            }
        }
        int index = Search.BisectRight(_noteStarts, secs) - 1;
        if (index >= 0 && secs < _noteEnds[index])
        {
            _lastNoteIndex = index;
            return index;
        }
        _lastNoteIndex = null;
        return null;
    }

    private int NearestNoteIndex(double secs)
    {
        int insertIndex = Search.BisectLeft(_noteStarts, secs);
        var candidates = new List<int>();
        if (insertIndex < NoteList.Count)
            candidates.Add(insertIndex);
        if (insertIndex > 0)
            candidates.Add(insertIndex - 1);
        if (candidates.Count == 0)
            return 0;
        return candidates.OrderBy(index => Math.Min(
            Math.Abs(NoteList[index].Start - secs),
            Math.Abs(secs - NoteList[index].End))).First();
    }
}

internal sealed class GaussianNode
{
    public double Origin { get; }
    public double Sigma { get; }
    public double Depth { get; }
    public double Start { get; }
    public double End { get; }

    public GaussianNode(double origin, double sigma, double depth)
    {
        Origin = origin;
        Sigma = sigma;
        Depth = depth;
        Start = origin - 5.0 * sigma;
        End = origin + 5.0 * sigma;
    }

    public double ValueAtSecs(double secs) =>
        Depth * Math.Exp(-0.5 * Math.Pow((secs - Origin) / Sigma, 2));
}

internal sealed class GaussianLayerGenerator
{
    private const double MaxBreak = 0.01;
    private const double PortamentoHalfwidthRatio = 0.3;
    private const double HalfwidthFloorSecs = 1.0 / 256;

    private readonly List<GaussianNode> _nodes = new();
    private readonly List<double> _starts = new();
    private readonly List<double> _ends = new();
    private (int, int)? _lastRange;

    private static double PortamentoSigma(double portamento) =>
        Math.Max(portamento * PortamentoHalfwidthRatio, HalfwidthFloorSecs);

    public GaussianLayerGenerator(List<NoteStruct> noteList)
    {
        if (noteList.Count == 0)
            return;
        var firstNote = noteList[0];
        double sigmaFirst = PortamentoSigma(firstNote.PortamentoLeft);
        _nodes.Add(new GaussianNode(firstNote.Start + 1.5 * sigmaFirst, sigmaFirst, -firstNote.DepthLeft * 100));
        for (int i = 0; i < noteList.Count - 1; i++)
        {
            var current = noteList[i];
            var next = noteList[i + 1];
            double sigmaL = PortamentoSigma(current.PortamentoRight);
            double sigmaR = PortamentoSigma(next.PortamentoLeft);
            if (next.Start - current.End >= MaxBreak)
            {
                _nodes.Add(new GaussianNode(current.End - 1.5 * sigmaL, sigmaL, -current.DepthRight * 100));
                _nodes.Add(new GaussianNode(next.Start + 1.5 * sigmaR, sigmaR, -next.DepthLeft * 100));
                continue;
            }
            double center = (current.End + next.Start) / 2 + next.PortamentoOffset;
            double depthLeft = current.DepthRight * 100;
            double depthRight = next.DepthLeft * 100;
            if (next.Key <= current.Key)
                depthRight = -depthRight;
            else
                depthLeft = -depthLeft;
            _nodes.Add(new GaussianNode(center - 1.5 * sigmaL, sigmaL, depthLeft));
            _nodes.Add(new GaussianNode(center + 1.5 * sigmaR, sigmaR, depthRight));
        }
        var lastNote = noteList[^1];
        double sigmaLast = PortamentoSigma(lastNote.PortamentoRight);
        _nodes.Add(new GaussianNode(lastNote.End - 1.5 * sigmaLast, sigmaLast, -lastNote.DepthRight * 100));
        _starts.AddRange(_nodes.Select(n => n.Start));
        _ends.AddRange(_nodes.Select(n => n.End));
    }

    public double PitchDiffAtSecs(double secs)
    {
        var (left, right) = ActiveRange(secs);
        double sum = 0;
        for (int index = left; index < right; index++)
            sum += _nodes[index].ValueAtSecs(secs);
        return sum;
    }

    private (int, int) ActiveRange(double secs)
    {
        if (_lastRange is { } range && RangeMatches(secs, range.Item1, range.Item2))
            return range;
        int left = Search.BisectRight(_ends, secs);
        int right = Search.BisectRight(_starts, secs);
        _lastRange = (left, right);
        return (left, right);
    }

    private bool RangeMatches(double secs, int left, int right)
    {
        double leftBoundary = left > 0 ? _ends[left - 1] : double.NegativeInfinity;
        double rightBoundary = right < _starts.Count ? _starts[right] : double.PositiveInfinity;
        return leftBoundary < secs && secs <= rightBoundary;
    }
}

internal sealed class VibratoNode
{
    public double Start { get; }
    public double End { get; }
    public double FadeLeft { get; }
    public double FadeRight { get; }
    public double Amplitude { get; }
    public double Frequency { get; }
    public double Phase { get; }

    public VibratoNode(double start, double end, double fadeLeft, double fadeRight, double amplitude, double frequency, double phase)
    {
        Start = start;
        End = end;
        FadeLeft = fadeLeft;
        FadeRight = fadeRight;
        Amplitude = amplitude;
        Frequency = frequency;
        Phase = phase;
    }

    public double ValueAtSecs(double secs)
    {
        double zoom;
        if (End - Start >= FadeLeft + FadeLeft)
        {
            if (secs < Start + FadeLeft)
                zoom = (secs - Start) / FadeLeft;
            else if (secs > End - FadeRight)
                zoom = (End - secs) / FadeRight;
            else
                zoom = 1.0;
        }
        else
        {
            double mid = (Start * FadeRight + End * FadeLeft) / (FadeLeft + FadeRight);
            zoom = secs < mid ? (secs - Start) / FadeLeft : (End - secs) / FadeRight;
        }
        return Amplitude * zoom * 100.0 * Math.Sin(2 * Math.PI * Frequency * (secs - Start) + Phase);
    }
}

internal sealed class VibratoLayerGenerator
{
    private const double MaxBreak = 0.01;

    private readonly List<VibratoNode> _nodes = new();
    private readonly List<double> _starts = new();
    private readonly List<double> _ends = new();
    private (int, int)? _lastRange;

    public VibratoLayerGenerator(List<NoteStruct> noteList)
    {
        for (int i = 0; i < noteList.Count; i++)
        {
            double start = noteList[i].Start;
            double end = noteList[i].End;
            if (end - start <= noteList[i].VibratoStart)
                continue;
            if (i < noteList.Count - 1 && noteList[i + 1].Start - end < MaxBreak)
            {
                end += Math.Min(
                    noteList[i + 1].PortamentoOffset,
                    Math.Min(noteList[i + 1].VibratoStart, noteList[i + 1].End - noteList[i + 1].Start));
            }
            if (start >= end)
                continue;
            _nodes.Add(new VibratoNode(
                start + noteList[i].VibratoStart,
                end,
                noteList[i].VibratoLeft,
                noteList[i].VibratoRight,
                noteList[i].VibratoDepth / 2,
                noteList[i].VibratoFrequency,
                noteList[i].VibratoPhase));
        }
        _starts.AddRange(_nodes.Select(n => n.Start));
        _ends.AddRange(_nodes.Select(n => n.End));
    }

    public double PitchDiffAtSecs(double secs)
    {
        var (left, right) = ActiveRange(secs);
        double sum = 0;
        for (int index = left; index < right; index++)
            sum += _nodes[index].ValueAtSecs(secs);
        return sum;
    }

    private (int, int) ActiveRange(double secs)
    {
        if (_lastRange is { } range && RangeMatches(secs, range.Item1, range.Item2))
            return range;
        int left = Search.BisectRight(_ends, secs);
        int right = Search.BisectRight(_starts, secs);
        _lastRange = (left, right);
        return (left, right);
    }

    private bool RangeMatches(double secs, int left, int right)
    {
        double leftBoundary = left > 0 ? _ends[left - 1] : double.NegativeInfinity;
        double rightBoundary = right < _starts.Count ? _starts[right] : double.PositiveInfinity;
        return leftBoundary < secs && secs <= rightBoundary;
    }
}

internal sealed class PitchControlLayerGenerator
{
    private readonly PitchControlIntervalMap _intervalDict = new();

    public PitchControlLayerGenerator(List<PitchControl> pitchControls)
    {
        foreach (var pitchControl in pitchControls)
        {
            if (pitchControl.Type == "curve" && pitchControl.Points.Count > 0)
            {
                var ticks = new List<int>();
                var values = new List<double>();
                foreach (var point in pitchControl.Points)
                {
                    int pointTicks = point.Offset + pitchControl.Pos;
                    double pointValue = (pitchControl.Pitch + point.Value) * 100;
                    if (ticks.Count > 0 && ticks[^1] == pointTicks)
                        values[^1] = pointValue;
                    else
                    {
                        ticks.Add(pointTicks);
                        values.Add(pointValue);
                    }
                }
                _intervalDict.AppendCurve(new PitchControlCurve(ticks, values));
            }
        }
    }

    public double? ValueAtTicks(int ticks) => _intervalDict.Get(ticks);
}

public sealed class SynthVPitchSimulator
{
    private readonly TimeSynchronizer _synchronizer;
    private readonly BaseLayerGenerator _baseLayer;
    private readonly GaussianLayerGenerator _gaussianLayer;
    private readonly VibratoLayerGenerator _vibratoLayer;
    private readonly PitchControlLayerGenerator? _pitchControlLayer;

    public SynthVPitchSimulator(
        TimeSynchronizer synchronizer,
        List<NoteStruct> noteList,
        List<PitchControl>? pitchControls = null)
    {
        _synchronizer = synchronizer;
        _baseLayer = new BaseLayerGenerator(noteList);
        _gaussianLayer = new GaussianLayerGenerator(noteList);
        _vibratoLayer = new VibratoLayerGenerator(noteList);
        if (pitchControls is { Count: > 0 })
            _pitchControlLayer = new PitchControlLayerGenerator(pitchControls);
    }

    public double PitchAtTicks(int ticks) =>
        PitchAtSecs(_synchronizer.GetActualSecsFromTicks(ticks), ticks);

    public List<double> PitchAtTicksBatch(List<int> ticksList)
    {
        if (ticksList.Count == 0)
            return new List<double>();
        var secsList = _synchronizer.GetActualSecsFromTicksBatch(ticksList);
        var result = new List<double>(ticksList.Count);
        for (int i = 0; i < ticksList.Count; i++)
            result.Add(PitchFromAlignedSecsAndTicks(secsList[i], ticksList[i]));
        return result;
    }

    public double PitchAtSecs(double secs, int? ticks = null)
    {
        int t = ticks ?? (int)Math.Round(_synchronizer.GetActualTicksFromSecs(secs));
        return PitchFromAlignedSecsAndTicks(secs, t);
    }

    private double PitchFromAlignedSecsAndTicks(double secs, int ticks)
    {
        if (_pitchControlLayer != null)
        {
            double? pitchControlValue = _pitchControlLayer.ValueAtTicks(ticks);
            if (pitchControlValue != null)
                return pitchControlValue.Value;
        }
        if (_baseLayer.NoteList.Count == 0)
            return 0.0;
        return _baseLayer.PitchAtSecs(secs)
            + _vibratoLayer.PitchDiffAtSecs(secs)
            + _gaussianLayer.PitchDiffAtSecs(secs);
    }
}
