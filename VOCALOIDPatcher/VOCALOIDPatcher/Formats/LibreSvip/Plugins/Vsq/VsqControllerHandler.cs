using System;
using System.Collections.Generic;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal static class VsqControllerHandler
{
    private const int InternalMin = -1000;
    private const int InternalMax = 1000;
    private const int InternalDefault = 0;

    private static (int MinValue, int MaxValue, int DefaultValue) GetParamBounds(string paramName) =>
        paramName switch
        {
            "dynamics" => (0, 127, 64),
            "breathiness" => (0, 127, 0),
            "brightness" => (0, 127, 0),
            "gender" => (0, 127, 64),
            _ => (0, 127, 0),
        };

    private static int MapExternalToInternal(int value, int defaultValue, int minValue, int maxValue, bool reverse = false)
    {
        int mapped;
        if (value >= defaultValue)
        {
            int denom = maxValue - defaultValue;
            mapped = denom == 0 ? InternalDefault
                : (int)Math.Round((double)(value - defaultValue) / denom * (InternalMax - InternalDefault) + InternalDefault);
        }
        else
        {
            int denom = defaultValue - minValue;
            mapped = denom == 0 ? InternalDefault
                : (int)Math.Round((double)(value - defaultValue) / denom * (InternalDefault - InternalMin) + InternalDefault);
        }
        if (reverse) mapped = -mapped;
        return Math.Max(InternalMin, Math.Min(InternalMax, mapped));
    }

    private static int MapInternalToExternal(int value, int defaultValue, int minValue, int maxValue, bool reverse = false)
    {
        int clamped = Math.Max(InternalMin, Math.Min(InternalMax, value));
        if (reverse) clamped = -clamped;
        int mapped;
        if (clamped >= InternalDefault)
        {
            int denom = InternalMax - InternalDefault;
            mapped = denom == 0 ? defaultValue
                : (int)Math.Round((double)(clamped - InternalDefault) / denom * (maxValue - defaultValue) + defaultValue);
        }
        else
        {
            int denom = InternalDefault - InternalMin;
            mapped = denom == 0 ? defaultValue
                : (int)Math.Round((double)(clamped - InternalDefault) / denom * (defaultValue - minValue) + defaultValue);
        }
        return Math.Max(minValue, Math.Min(maxValue, mapped));
    }

    private static void AppendStepPoints(List<Point> points, int pos, int prevValue, int curValue)
    {
        int boundaryPos = pos - 1;
        if (curValue != prevValue && boundaryPos >= 0)
        {
            var boundary = new Point(boundaryPos, prevValue);
            if (points.Count == 0 || points[^1] != boundary)
                points.Add(boundary);
        }
        var cur = new Point(pos, curValue);
        if (points.Count == 0 || points[^1] != cur)
            points.Add(cur);
    }

    public static List<Point> ConvertVocaloidCurveToParamPoints(
        ControllerCurve curve, int positionOffset = 0, bool reverseValue = false)
    {
        if (curve.IsEmpty)
            return new List<Point>();
        var (minValue, maxValue, defaultValue) = GetParamBounds(curve.Name);
        var points = new List<Point>();
        int prevInternal = MapExternalToInternal(curve.DefaultValue, defaultValue, minValue, maxValue, reverseValue);
        foreach (var ev in curve.Events)
        {
            int curPos = ev.Pos + positionOffset;
            int curInternal = MapExternalToInternal(ev.Value, defaultValue, minValue, maxValue, reverseValue);
            AppendStepPoints(points, curPos, prevInternal, curInternal);
            prevInternal = curInternal;
        }
        return points;
    }

    private static List<Point> CollapseStepPoints(List<Point> points)
    {
        if (points.Count <= 2) return points;
        var result = new List<Point>();
        int i = 0;
        while (i < points.Count)
        {
            var point = points[i];
            var next = i + 1 < points.Count ? (Point?)points[i + 1] : null;
            if (next != null && next.Value.X == point.X + 1 && next.Value.Y != point.Y)
            {
                bool prevMatch = result.Count > 0 && result[^1].Y == point.Y;
                bool isFirst = i == 0 && point.Y == 0;
                if (isFirst || prevMatch)
                {
                    result.Add(next.Value);
                    i += 2;
                    continue;
                }
            }
            if (result.Count == 0 || result[^1] != point)
                result.Add(point);
            i++;
        }
        return result;
    }

    public static ControllerCurve ConvertParamPointsToVocaloidCurve(
        List<Point> points, string paramName, int positionOffset = 0, bool reverseValue = false)
    {
        var (minValue, maxValue, defaultValue) = GetParamBounds(paramName);
        var realPoints = new List<Point>();
        foreach (var p in points)
            if (p.X != Point.StartX && p.X != Point.EndX)
                realPoints.Add(p);
        realPoints = CollapseStepPoints(realPoints);
        var events = new List<ControllerEvent>();
        foreach (var p in realPoints)
        {
            int extVal = MapInternalToExternal(p.Y, defaultValue, minValue, maxValue, reverseValue);
            events.Add(new ControllerEvent(p.X + positionOffset, extVal));
        }
        return new ControllerCurve(paramName, events, defaultValue, minValue, maxValue);
    }
}
