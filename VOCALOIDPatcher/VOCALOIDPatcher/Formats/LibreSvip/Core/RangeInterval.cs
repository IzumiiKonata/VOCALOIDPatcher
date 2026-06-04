using System.Collections.Generic;
using System.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public sealed class RangeInterval
{
    private readonly List<(int Start, int End)> _ranges;

    public RangeInterval(IEnumerable<(int Start, int End)>? ranges = null)
    {
        _ranges = Merge(ranges ?? new List<(int, int)>());
    }

    private static List<(int, int)> Merge(IEnumerable<(int Start, int End)> ranges)
    {
        var sorted = ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start).ToList();
        var result = new List<(int, int)>();
        foreach (var (start, end) in sorted)
        {
            if (result.Count > 0 && start <= result[^1].Item2)
                result[^1] = (result[^1].Item1, System.Math.Max(result[^1].Item2, end));
            else
                result.Add((start, end));
        }
        return result;
    }

    public RangeInterval Expand(int radius) =>
        new(_ranges.Select(r => (r.Start - radius, r.End + radius)));

    public RangeInterval Shift(int offset) =>
        new(_ranges.Select(r => (r.Start + offset, r.End + offset)));

    public IReadOnlyList<(int Start, int End)> SubRanges() => _ranges;
}
