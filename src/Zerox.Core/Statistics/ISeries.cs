using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// What the aggregator and the remote provider see of a series.
public interface ISeries
{
    string Name { get; }

    HierId Id { get; }

    // Samples added since the last Clear (the stored ones are at most the capacity).
    long Count { get; }

    // The stored points added after the first `count` samples (all the stored ones when count is
    // greater than Count, which happens after a Clear).
    SeriesUpdate UpdateSince(long count);
}

// A series that takes one value per sample (the aggregator adds to it by name).
public interface IValueSeries<in TValue> : ISeries
{
    void Add(TValue value);
}

// Points is the series' own point array: KeyValuePair<TKey, TValue>[], KeyValuePair<TKey, ImmutableArray<TValue>>[] or TValue[].
public sealed record SeriesUpdate(string SeriesName, long Count, Array Points);

internal static class SeriesWindow
{
    // How many of the last stored points were added after the first `since` samples.
    public static int NewPoints(long since, long count, int stored)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(since);
        return since > count ? stored : (int)Math.Min(count - since, stored);
    }
}
