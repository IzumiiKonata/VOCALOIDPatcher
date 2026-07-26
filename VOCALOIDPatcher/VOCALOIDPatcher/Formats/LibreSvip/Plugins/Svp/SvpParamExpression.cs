using System;
using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Svp;

public interface IParamExpression
{
    double ValueAtTicks(int ticks);
}

public sealed class SumParamExpression : IParamExpression
{
    private readonly IParamExpression _left;
    private readonly IParamExpression _right;

    public SumParamExpression(IParamExpression left, IParamExpression right)
    {
        _left = left;
        _right = right;
    }

    public double ValueAtTicks(int ticks) => _left.ValueAtTicks(ticks) + _right.ValueAtTicks(ticks);
}

public sealed class MaskedParamExpression : IParamExpression
{
    private readonly IParamExpression _source;
    private readonly IReadOnlyList<(int Start, int End)> _ranges;

    public MaskedParamExpression(IParamExpression source, IEnumerable<(int Start, int End)> ranges)
    {
        _source = source;
        _ranges = new RangeInterval(ranges).SubRanges();
    }

    public double ValueAtTicks(int ticks)
    {
        foreach (var (start, end) in _ranges)
        {
            if (ticks < start)
                return 0;
            if (ticks < end)
                return _source.ValueAtTicks(ticks);
        }
        return 0;
    }
}

public sealed class CurveGenerator : IParamExpression
{
    private readonly List<Point> _pointList = new();
    private readonly List<int> _positions = new();
    private readonly InterpolationFunc _interpolation;
    private readonly int _baseValue;

    public CurveGenerator(IEnumerable<Point> points, InterpolationFunc interpolation, int baseValue)
    {
        _interpolation = interpolation;
        _baseValue = baseValue;
        int currentPos = -1;
        long currentSum = 0;
        int overlap = 0;
        foreach (var p in points)
        {
            if (p.X != currentPos && currentPos >= 0)
            {
                _pointList.Add(new Point(currentPos, (int)Math.Round((double)currentSum / overlap)));
                currentSum = 0;
                overlap = 0;
            }
            currentSum += p.Y;
            overlap++;
            currentPos = p.X;
        }
        if (currentPos != -1)
            _pointList.Add(new Point(currentPos, (int)Math.Round((double)currentSum / overlap)));
        _positions.AddRange(_pointList.Select(p => p.X));
    }

    public double ValueAtTicks(int ticks)
    {
        if (_pointList.Count == 0)
            return _baseValue;
        int idx = Search.BisectRight(_positions, ticks) - 1;
        if (idx < 0)
            return _pointList[0].Y;
        if (idx >= _pointList.Count - 1)
            return _pointList[^1].Y;
        return _interpolation(ticks, (_pointList[idx].X, _pointList[idx].Y), (_pointList[idx + 1].X, _pointList[idx + 1].Y));
    }
}

public sealed class PitchGenerator : IParamExpression
{
    private readonly TimeSynchronizer _synchronizer;
    private readonly IParamExpression _pitchDiff;
    private readonly IParamExpression _vibratoEnv;
    private readonly SynthVPitchSimulator _pitchSimulator;

    public PitchGenerator(TimeSynchronizer synchronizer, List<NoteStruct> noteStructs,
        IParamExpression pitchDiff, IParamExpression vibratoEnv, List<PitchControl>? pitchControls)
    {
        _synchronizer = synchronizer;
        _pitchDiff = pitchDiff;
        _vibratoEnv = vibratoEnv;
        _pitchSimulator = new SynthVPitchSimulator(synchronizer, noteStructs, pitchControls);
    }

    public double ValueAtTicks(int ticks) =>
        ValueAtSecs(_synchronizer.GetActualSecsFromTicks(ticks));

    public double ValueAtSecs(double secs)
    {
        int ticks = (int)Math.Round(_synchronizer.GetActualTicksFromSecs(secs));
        return _pitchSimulator.PitchAtSecs(secs, ticks)
            + _pitchDiff.ValueAtTicks(ticks) * _vibratoEnv.ValueAtTicks(ticks) / 1000;
    }
}
