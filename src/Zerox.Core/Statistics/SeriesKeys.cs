namespace Zerox.Core.Statistics;

// Where the key of a new sample comes from, and how far two keys are (the unit of a rate).
public interface ISeriesKeys<TKey>
    where TKey : struct, IComparable<TKey>
{
    TKey NextKey();

    double Distance(TKey earlier, TKey later);
}

// UTC time keys that never go back: the start time is read once, then the provider's timestamp moves them.
public sealed class TimeKeys : ISeriesKeys<DateTime>
{
    private readonly TimeProvider _time;
    private readonly DateTime _start;
    private readonly long _startTimestamp;

    public TimeKeys(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _time = timeProvider;
        _start = timeProvider.GetUtcNow().UtcDateTime;
        _startTimestamp = timeProvider.GetTimestamp();
    }

    public DateTime Now => _start + _time.GetElapsedTime(_startTimestamp);

    public DateTime NextKey() => Now;

    // Seconds: a rate over time keys is per second.
    public double Distance(DateTime earlier, DateTime later) => (later - earlier).TotalSeconds;
}

// Counter keys start, start + 1, ...; distance in steps.
public sealed class IndexKeys(long start = 0) : ISeriesKeys<long>
{
    private long _next = start;

    public long NextKey() => Interlocked.Increment(ref _next) - 1;

    public double Distance(long earlier, long later) => later - earlier;
}
