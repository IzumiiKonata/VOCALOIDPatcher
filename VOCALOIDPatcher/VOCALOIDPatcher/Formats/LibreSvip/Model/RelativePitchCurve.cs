using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Model;

public sealed class RelativePitchCurve
{
    private readonly int _firstBarLength;
    private readonly double _lowerBound;
    private readonly double _upperBound;
    private readonly int _pitchInterval;
    private readonly bool _isStaircase;

    public RelativePitchCurve(
        int firstBarLength = Core.Constants.TicksInBeat * 4,
        double lowerBound = 0.0,
        double upperBound = double.PositiveInfinity,
        int pitchInterval = 5,
        bool isStaircase = true)
    {
        _firstBarLength = firstBarLength;
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _pitchInterval = pitchInterval;
        _isStaircase = isStaircase;
    }

    public ParamCurve ToAbsolute(List<Point> points, PitchSimulator pitchSimulator) =>
        new() { Points = ConvertRelativity(points, pitchSimulator, toAbsolute: true) };

    public List<Point> FromAbsolute(List<Point> points, PitchSimulator pitchSimulator) =>
        ConvertRelativity(points, pitchSimulator, toAbsolute: false);

    private List<Point> ConvertRelativity(List<Point> points, PitchSimulator pitchSimulator, bool toAbsolute)
    {
        int pitchOffset = toAbsolute ? 0 : -_firstBarLength;
        int convertedOffset = toAbsolute ? _firstBarLength : -_firstBarLength;
        var pointTickPositions = new List<int>(points.Count);
        foreach (var point in points)
            pointTickPositions.Add(point.X + pitchOffset);
        var pitchValues = pitchSimulator.PitchAtTicksBatch(pointTickPositions);
        var pitchValueMap = new Dictionary<int, double?>();
        for (int i = 0; i < pointTickPositions.Count; i++)
            pitchValueMap[pointTickPositions[i]] = pitchValues[i];

        double? GetPitchValue(int tickPos)
        {
            if (!pitchValueMap.TryGetValue(tickPos, out var v))
            {
                v = pitchSimulator.PitchAtTicks(tickPos);
                pitchValueMap[tickPos] = v;
            }
            return v;
        }

        IEnumerable<(int Tick, double? Key)> IterInterpolatedPitches(int startTick, int endTick)
        {
            for (int tick = startTick + _pitchInterval; tick < endTick; tick += _pitchInterval)
                yield return (tick, GetPitchValue(toAbsolute ? tick - _firstBarLength : tick));
        }

        var convertedData = new List<Point>();
        int? prevX = null;
        double? prevY = null;
        foreach (var point in points)
        {
            int pos = point.X + pitchOffset;
            int curX = point.X + convertedOffset;
            double? baseKey = GetPitchValue(pos);
            double? y;
            double? relY;
            if (baseKey == null)
            {
                y = null;
                relY = null;
            }
            else if (!toAbsolute && point.Y == -100)
            {
                if (point.X == Point.StartX || point.X == Point.EndX)
                    continue;
                y = 0.0;
                relY = y;
            }
            else
            {
                relY = toAbsolute ? point.Y : point.Y - baseKey;
                y = toAbsolute ? relY + baseKey : relY;
            }
            if (y != null && prevX != null && prevY != null && convertedData.Count > 0
                && curX - prevX.Value > _pitchInterval)
            {
                foreach (var (tick, tickKey) in IterInterpolatedPitches(prevX.Value, curX))
                {
                    if (tickKey == null)
                        break;
                    if (toAbsolute)
                    {
                        double interpolatedRelY;
                        if (_isStaircase || relY == null)
                            interpolatedRelY = prevY.Value;
                        else
                            interpolatedRelY = prevY.Value + (relY.Value - prevY.Value) * (tick - prevX.Value) / (curX - prevX.Value);
                        convertedData.Add(new Point(tick, (int)Math.Round(interpolatedRelY + tickKey.Value)));
                    }
                    else if (!(_isStaircase && prevY == y))
                    {
                        convertedData.Add(new Point(tick,
                            (int)Math.Round(prevY.Value + (y.Value - prevY.Value) * (tick - prevX.Value) / (curX - prevX.Value))));
                    }
                }
            }
            if (y != null)
            {
                if (toAbsolute && prevY == null)
                    convertedData.Add(new Point(curX, -100));
                convertedData.Add(new Point(curX, (int)Math.Round(y.Value)));
                if (toAbsolute)
                {
                    if (point.Y != 0)
                    {
                        prevY = relY;
                    }
                    else
                    {
                        convertedData.Add(new Point(curX, -100));
                        prevY = null;
                    }
                }
                else
                {
                    prevY = point.Y == -100 ? null : y;
                }
            }
            else
            {
                prevY = null;
            }
            prevX = curX;
        }
        if (convertedData.Count > 0 && toAbsolute)
        {
            if (_lowerBound == 0)
                convertedData.Insert(0, Point.StartPoint());
            if (double.IsPositiveInfinity(_upperBound))
            {
                convertedData.Add(convertedData[^1].WithY(-100));
                convertedData.Add(Point.EndPoint());
            }
        }
        return convertedData;
    }
}
