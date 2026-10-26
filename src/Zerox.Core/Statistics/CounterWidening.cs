using System.Collections.Immutable;

namespace Zerox.Core.Statistics;

// Hardware counters of 16 or 32 bits wrap around; widened against the previous value they keep growing.
public static class CounterWidening
{
    public static long Widen(ushort raw, long last)
        => (last & ~0xFFFFL) + raw + ((ushort)last > raw ? 0x1_0000L : 0);

    public static long Widen(uint raw, long last)
        => (last & ~0xFFFF_FFFFL) + raw + ((uint)last > raw ? 0x1_0000_0000L : 0);

    // One producer per series: reads Last, then adds the widened row.
    public static void AddCounters<TKey>(this ArraySeries<TKey, long> series, ReadOnlySpan<ushort> raw)
        where TKey : struct, IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(series);
        var last = LastOf(series, raw.Length);
        var row = new long[raw.Length];
        for (var i = 0; i < row.Length; i++)
            row[i] = Widen(raw[i], last[i]);
        series.Add(row);
    }

    public static void AddCounters<TKey>(this ArraySeries<TKey, long> series, ReadOnlySpan<uint> raw)
        where TKey : struct, IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(series);
        var last = LastOf(series, raw.Length);
        var row = new long[raw.Length];
        for (var i = 0; i < row.Length; i++)
            row[i] = Widen(raw[i], last[i]);
        series.Add(row);
    }

    private static ImmutableArray<long> LastOf<TKey>(ArraySeries<TKey, long> series, int length)
        where TKey : struct, IComparable<TKey>
    {
        if (length != series.Width)
            throw new ArgumentException($"A row of {series.Name} has {series.Width} values, not {length}", nameof(length));
        return series.Last;
    }
}
