using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

// 移植自 libresvip/utils/search.py 中本移植用到的部分
public static class Search
{
    public static int FindIndex<T>(IReadOnlyList<T> list, Func<T, bool> pred)
    {
        for (int i = 0; i < list.Count; i++)
            if (pred(list[i]))
                return i;
        return -1;
    }

    public static int FindLastIndex<T>(IReadOnlyList<T> list, Func<T, bool> pred)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (pred(list[i]))
                return i;
        return -1;
    }

    // 等价于 Python bisect.bisect_right
    public static int BisectRight(IReadOnlyList<int> sorted, int value)
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

    public static int BisectRight(IReadOnlyList<double> sorted, double value)
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
}
