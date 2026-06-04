using System.Collections.Generic;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public sealed class TimeSynchronizer
{
    private readonly bool _isAbsoluteTimeCode;
    private readonly double _defaultTempo;
    private readonly List<SongTempo> _tempoList;
    private readonly List<int> _positions;
    private readonly List<double> _cumSecs;

    public TimeSynchronizer(
        List<SongTempo> oriTempoList,
        int skipTicks = 0,
        bool isAbsoluteTimeCode = false,
        double defaultTempo = 60)
    {
        _tempoList = skipTicks > 0
            ? TickCounter.SkipTempoList(oriTempoList, skipTicks)
            : oriTempoList;
        _isAbsoluteTimeCode = isAbsoluteTimeCode;
        _defaultTempo = defaultTempo;

        int n = _tempoList.Count;
        _positions = _tempoList.Select(t => t.Position).ToList();
        _cumSecs = Enumerable.Repeat(0.0, n).ToList();
        for (int i = 1; i < n; i++)
        {
            int dt = _positions[i] - _positions[i - 1];
            _cumSecs[i] = _cumSecs[i - 1] + dt / _tempoList[i - 1].Bpm / 8;
        }
    }

    public IReadOnlyList<SongTempo> TempoList => _tempoList;

    private double SecsAtTick(double ticks)
    {
        int idx = BisectRight(_positions, ticks) - 1;
        if (idx < 0)
            idx = 0;
        return _cumSecs[idx] + (ticks - _positions[idx]) / _tempoList[idx].Bpm / 8;
    }

    private double TicksAtSecs(double secs)
    {
        int idx = BisectRight(_cumSecs, secs) - 1;
        if (idx < 0)
            idx = 0;
        return _positions[idx] + (secs - _cumSecs[idx]) * _tempoList[idx].Bpm * 8;
    }

    public double GetActualTicksFromTicks(double ticks)
    {
        if (!_isAbsoluteTimeCode)
            return ticks;
        double res = 0.0;
        int idx = BisectRight(_positions, ticks) - 1;
        if (idx < 0)
            idx = 0;
        for (int i = 0; i < idx; i++)
            res += (_positions[i + 1] - _positions[i]) * (_defaultTempo / _tempoList[i].Bpm);
        res += (ticks - _positions[idx]) * _defaultTempo / _tempoList[idx].Bpm;
        return res;
    }

    public double GetDurationSecsFromTicks(double startTicks, double endTicks)
    {
        if (_isAbsoluteTimeCode)
            return (GetActualTicksFromTicks(endTicks) - GetActualTicksFromTicks(startTicks)) / _defaultTempo / 8;
        return SecsAtTick(endTicks) - SecsAtTick(startTicks);
    }

    public double GetActualTicksFromSecsOffset(double startTicks, double offsetSecs)
    {
        if (_isAbsoluteTimeCode)
            return GetActualTicksFromTicks(startTicks) + offsetSecs * _defaultTempo * 8;
        double targetSecs = SecsAtTick(startTicks) + offsetSecs;
        return TicksAtSecs(targetSecs);
    }

    public double GetActualTicksFromSecs(double secs) => GetActualTicksFromSecsOffset(0, secs);

    public double GetActualSecsFromTicks(double ticks) => GetDurationSecsFromTicks(0, ticks);

    public List<double> GetActualSecsFromTicksBatch(List<int> ticksList)
    {
        if (ticksList.Count == 0)
            return new List<double>();
        var indexed = ticksList.Select((t, i) => (i, t)).OrderBy(x => x.t).ToList();
        var results = Enumerable.Repeat(0.0, ticksList.Count).ToList();
        int segIdx = 0;
        int n = _positions.Count;
        foreach (var (origIdx, ticks) in indexed)
        {
            while (segIdx + 1 < n && _positions[segIdx + 1] <= ticks)
                segIdx++;
            results[origIdx] = _cumSecs[segIdx] + (ticks - _positions[segIdx]) / _tempoList[segIdx].Bpm / 8;
        }
        return results;
    }

    private static int BisectRight(List<int> sorted, double value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (value < sorted[mid])
                hi = mid;
            else
                lo = mid + 1;
        }
        return lo;
    }

    private static int BisectRight(List<double> sorted, double value) =>
        Search.BisectRight(sorted, value);
}
