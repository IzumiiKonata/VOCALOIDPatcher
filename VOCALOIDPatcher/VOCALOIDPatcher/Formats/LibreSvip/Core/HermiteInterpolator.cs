using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public sealed class HermiteInterpolator
{
    private readonly List<(double X, double Y)> _points;

    public HermiteInterpolator(List<(double X, double Y)> points) => _points = points;

    private static double F1(double t1, double t2) => (1 + 2 * t1) * t2 * t2;

    private static double F3(double t, double d) => t * t * d;

    private static double Slope((double X, double Y) p1, (double X, double Y) p2) =>
        p2.X != p1.X ? (p2.Y - p1.Y) / (p2.X - p1.X) : double.NaN;

    private double SlopeAt(int pointIndex)
    {
        if (pointIndex == 0 || pointIndex == _points.Count - 1)
            return 0;
        var point = _points[pointIndex];
        double lastK = Slope(point, _points[pointIndex - 1]);
        double nextK = Slope(point, _points[pointIndex + 1]);
        double kk = lastK * nextK;
        return kk <= 0 ? 0 : 2 / (1 / lastK + 1 / nextK);
    }

    public List<double> Interpolate(List<double> xs)
    {
        var ys = new List<double>();
        if (_points.Count < 2)
        {
            double v = _points.Count == 1 ? _points[0].Y : 0;
            foreach (var _ in xs)
                ys.Add(v);
            return ys;
        }
        if (_points.Count == 2)
        {
            foreach (var x in xs)
                ys.Add(MusicMath.LinearInterpolation(x, _points[0], _points[1]));
            return ys;
        }
        int pointIndex = 0;
        int lastDeltaIndex = -1;
        double lastDelta = 0.0;
        foreach (var x in xs)
        {
            while (pointIndex < _points.Count && _points[pointIndex].X < x)
                pointIndex++;
            if (pointIndex == 0)
                ys.Add(_points[0].Y);
            else if (pointIndex == _points.Count)
                ys.Add(_points[^1].Y);
            else
            {
                var lastPoint = _points[pointIndex - 1];
                if (pointIndex != lastDeltaIndex)
                {
                    lastDelta = SlopeAt(pointIndex - 1);
                    lastDeltaIndex = pointIndex;
                }
                var nextPoint = _points[pointIndex];
                double nextDelta = SlopeAt(pointIndex);
                double delta1 = x - lastPoint.X;
                double delta2 = x - nextPoint.X;
                double t1 = delta1 / (nextPoint.X - lastPoint.X);
                double t2 = delta2 / (lastPoint.X - nextPoint.X);
                ys.Add(
                    F1(t1, t2) * lastPoint.Y
                    + F1(t2, t1) * nextPoint.Y
                    + F3(t2, delta1) * lastDelta
                    + F3(t1, delta2) * nextDelta);
            }
        }
        return ys;
    }
}
