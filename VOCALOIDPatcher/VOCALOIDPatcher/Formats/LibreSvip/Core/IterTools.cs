using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public static class IterTools
{
    public static List<List<T>> SplitWhen<T>(IReadOnlyList<T> items, Func<T, T, bool> pred)
    {
        var result = new List<List<T>>();
        if (items.Count == 0)
            return result;
        var current = new List<T> { items[0] };
        for (int i = 1; i < items.Count; i++)
        {
            if (pred(items[i - 1], items[i]))
            {
                result.Add(current);
                current = new List<T> { items[i] };
            }
            else
            {
                current.Add(items[i]);
            }
        }
        result.Add(current);
        return result;
    }

    public static List<List<T>> SplitBefore<T>(IReadOnlyList<T> items, Func<T, bool> pred)
    {
        var result = new List<List<T>>();
        var current = new List<T>();
        foreach (var item in items)
        {
            if (pred(item) && current.Count > 0)
            {
                result.Add(current);
                current = new List<T>();
            }
            current.Add(item);
        }
        if (current.Count > 0)
            result.Add(current);
        return result;
    }

    public static IEnumerable<T> UniqueJustSeen<T, TKey>(IEnumerable<T> items, Func<T, TKey> keySelector)
        where TKey : IEquatable<TKey>
    {
        bool hasPrev = false;
        TKey prevKey = default!;
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!hasPrev || !key.Equals(prevKey))
            {
                yield return item;
                prevKey = key;
                hasPrev = true;
            }
        }
    }

    public static List<double> NumericRange(double start, double stop, double step)
    {
        var result = new List<double>();
        if (step == 0)
            return result;
        if (step > 0)
            for (double x = start; x < stop; x += step)
                result.Add(x);
        else
            for (double x = start; x > stop; x += step)
                result.Add(x);
        return result;
    }

    public static IEnumerable<(TKey Key, List<TVal> Values)> GroupByTransform<T, TKey, TVal>(
        IEnumerable<T> items, Func<T, TKey> keyFunc, Func<T, TVal> valueFunc)
        where TKey : IEquatable<TKey>
    {
        bool hasPrev = false;
        TKey prevKey = default!;
        var current = new List<TVal>();
        foreach (var item in items)
        {
            var key = keyFunc(item);
            if (!hasPrev)
            {
                prevKey = key;
                hasPrev = true;
            }
            else if (!key.Equals(prevKey))
            {
                yield return (prevKey, current);
                current = new List<TVal>();
                prevKey = key;
            }
            current.Add(valueFunc(item));
        }
        if (hasPrev)
            yield return (prevKey, current);
    }
}
