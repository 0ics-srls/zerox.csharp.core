namespace Zerox.Core.Collections;

// A window over a sorted sequence: From inclusive, To exclusive (null = unbounded), then only the
// last MaxLastCount elements of what falls inside.
public readonly record struct Interval<T>(T? From, T? To, int? MaxLastCount = null)
    where T : struct;

// The factories (static members live on a non-generic class, CA1000).
public static class Interval
{
    public static Interval<T> Range<T>(T from, T to, int? maxLastCount = null) where T : struct
        => new(from, to, maxLastCount);

    public static Interval<T> Since<T>(T from, int? maxLastCount = null) where T : struct
        => new(from, null, maxLastCount);

    public static Interval<T> LastN<T>(int count) where T : struct
        => new(null, null, count);

    // The time window of length span ending at now (the caller's clock).
    public static Interval<DateTime> Last(TimeSpan span, DateTime now, int? maxLastCount = null)
        => new(now - span, null, maxLastCount);
}
