# Series

<block id="02-series-keys">
## TDDAB-2: Write the Keys of the Series Samples

<intro>
Delivers where the key of a new sample comes from and how far two keys are, in
`src/Zerox.Core/Statistics/SeriesKeys.cs`:
- `ISeriesKeys<TKey>`: `TKey NextKey()`, `double Distance(TKey earlier, TKey later)` (the unit of the
  rate: a series' rate is value change per distance unit);
- `TimeKeys(TimeProvider)`: `DateTime` keys in UTC, MONOTONIC (start time read once, then the
  provider's timestamp: a wall clock set back never makes keys go back); `Now`; distance in
  seconds (so a rate is per second, as the old `× 1000 / ms`);
- `IndexKeys(long start = 0)`: `long` keys `start, start + 1, …` (thread safe); distance in steps.

Capability: old `ISeriesKeyProvider<KT>` + `DateTimeKeyProvider` (`DateTime.Now`, local time,
not monotonic) in `../../tmp/zerox/Zerox.Wpf.Core/Core/Data/`, and the `GetKeyDelta` copied in
every series class (milliseconds, 0 replaced by 1). The interface stays because the series
consume several implementations (time keys in production, index keys and stepping keys in tests).

Files: `src/Zerox.Core/Statistics/SeriesKeys.cs`; tests
`tests/Zerox.Core.Tests/Statistics/SeriesKeysTests.cs`.
</intro>

<red>
- test: Should_FollowTheClock_WhenTheTimeAdvances — `FakeTimeProvider` at 2026-01-01T00:00:00Z: `NextKey()` is that instant (Kind `Utc`); after `Advance(1.5 s)` `NextKey()` is 1.5 s later and equals `Now`
- test: Should_MeasureSeconds_WhenTimeKeysCompareTwoKeys — `Distance(t, t + 250 ms)` is 0.25; `Distance(t + 1 s, t)` is −1
- test: Should_CountFromTheStart_WhenIndexKeysAreAsked — `new IndexKeys(5)`: `NextKey()` gives 5, 6, 7; `Distance(3, 10)` is 7
- test: Should_GiveUniqueKeys_WhenManyThreadsAsk — 8 parallel loops take 1000 keys each from one `IndexKeys`: 8000 distinct keys, `0..7999`
</red>

### Implementation

```csharp
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
```

<success>
- [ ] `SeriesKeysTests` pass (4 tests)
- [ ] `grep -rn "DateTime.Now\|DateTime.UtcNow" src/Zerox.Core/Statistics` is empty (time only from the `TimeProvider`)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="03-data-series">
## TDDAB-3: Write the One Numeric Series

<intro>
Delivers `DataSeries<TKey, TValue>` (`TKey : struct, IComparable<TKey>`, `TValue : struct,
INumber<TValue>`): a named series that keeps the last `capacity` points (default 1000) in a
`CircularBuffer<KeyValuePair<TKey, TValue>>` and running statistics over every sample added since
the last `Clear`: `Count` (`long`), `Min`, `Max`, `Last`, `LastPair`, `Sum` and `Mean`
(`double`), `Rate` (value change per key distance unit between the last two samples). Also the
contracts the aggregator and the remote provider see of every series, in
`src/Zerox.Core/Statistics/ISeries.cs`: `ISeries` (`Name`, `Id`, `Count`,
`UpdateSince(long count)`), `IValueSeries<in TValue>` (`Add(TValue)`), the record
`SeriesUpdate(string SeriesName, long Count, Array Points)`, and the internal helper
`SeriesWindow.NewPoints(since, count, stored)` shared by the three series.

Rules of the series:
- `Add(value)` takes its key from the `ISeriesKeys<TKey>` given to the constructor, under the
  series' `Lock` (keys stay ascending); `Add(value, key)` takes an explicit key; a key lower than
  the last throws `ArgumentOutOfRangeException` (the buffer must stay sorted);
- the first sample sets `Min` = `Max` = value and `Rate` = 0; a later sample at distance > 0 sets
  `Rate = (value − last) / distance` computed in `double` (no integer division, no unsigned
  wrap); at distance 0 the rate keeps its previous value;
- empty series: `Count` 0, `Min` = `Max` = `Last` = 0, `Sum` = `Mean` = `Rate` = 0;
- `View(Interval<TKey>)` returns a copy (half-open, last N), `ViewSum(from, to)` the `double` sum
  of the half-open range, `Get(index)` the stored point at `index` (0 = oldest stored,
  `ArgumentOutOfRangeException` outside), `StoredCount`, `Capacity`;
- `Snapshot()` returns the stored points; `Load(points)` replaces everything (statistics
  recomputed from the points; keys must be ascending, otherwise `ArgumentException` and the series
  is unchanged); persistence is the consumer's;
- `Id` is the `HierId` given to the constructor, or `HierId.From(name)`;
- `UpdateSince(count)`: the stored points added after the first `count` samples (all the stored
  points when `count` > `Count`, which happens after a `Clear`; negative → `ArgumentOutOfRangeException`).

Capability: old `DataSeriesBase<KT,VT>` (`../../tmp/zerox/Zerox.Wpf.Core/Core/Data/DataSeriesBase.cs`),
`DateTimeSeries<VT>`, `DoubleDatetimeSeries`, `Int32DatetimeSeries`, the persistor interfaces in
`IDataSeriesTP.cs`; tests in `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/DataTests.cs`
(`TestRateOfSeries`, `TestSeriesView`, `SeriesDataPersistorInt32Int`,
`SeriesDataPersistorDatetimeDouble`, `SeriesDataFirstLoadAndUpdate`). Generic math replaces the
`TypeCode` switch and the per-type subclasses. Bugs fixed: integer rate division, first rate
against a default key, buffer add failure written to `Trace` and ignored, `Get` copying the whole
buffer, keys going back accepted. `Medium` is named `Mean`. The old persistence tests become
snapshot tests (a `Dictionary<string, KeyValuePair<…>[]>` in the test plays the persistor; the
Mongo tests are dropped). Old `Int32IntSeries` = `DataSeries<long, int>` with `IndexKeys`;
`DatetimeDoubleSeries` = `DataSeries<DateTime, double>` with `TimeKeys` on a `FakeTimeProvider`
advanced 1 ms per sample (distinct keys); `TestSeriesView` runs on the fake clock (no sleep).

Files: `src/Zerox.Core/Statistics/ISeries.cs`, `src/Zerox.Core/Statistics/DataSeries.cs`; tests
`tests/Zerox.Core.Tests/Statistics/DataSeriesTests.cs`.
</intro>

<red>
- test: TestRateOfSeries — keys stepping 1 s (a test `ISeriesKeys<DateTime>`), values `0, −10, …, −90`: after each value but the first `Rate` is −10 (1e-9); a second series fed with those rates has `Count` 9 and `Mean` −10
- test: TestSeriesView — `TimeKeys` on a fake clock, 20 values one every 45 ms, `start` = the clock just before the 11th value: `View(Interval.Range(start, end))` has 10 points, the first ≥ `start`, the last ≤ `end`
- test: SeriesDataPersistorInt32Int — two `DataSeries<long, int>` (capacity 10 000) with `0..9999`; the second's `Snapshot()` saved; both cleared and refilled with 1; only the second `Load`s: views of the last 5000 give first/last values 1/1 and 5000/9999, `Sum` 10 000 and 49 995 000, `Min` 1 and 0, `Max` 1 and 9999, `Count` 10 000 both, first key < last key
- test: SeriesDataPersistorDatetimeDouble — the same with `DataSeries<DateTime, double>` on `TimeKeys` (fake clock + 1 ms per sample)
- test: SeriesDataFirstLoadAndUpdate — `0..9999` saved, loaded (`Count` 10 000), 999 added, saved again: the last saved value is 999
- test: Should_ReportZero_WhenTheSeriesIsEmpty — `Count`, `Min`, `Max`, `Last`, `Sum`, `Mean`, `Rate` are 0, `StoredCount` 0, `View` of everything empty
- test: Should_StartTheRateAtZero_WhenTheFirstValueArrives — first value 50 at key 1000: `Rate` 0, `Min` = `Max` = 50
- test: Should_KeepTheFractionOfTheRate_WhenTheValuesAreIntegers — `DataSeries<long, int>`: 10 at key 0, 11 at key 2 → `Rate` 0.5 (the old integer division gave 0)
- test: Should_ComputeANegativeRate_WhenAnUnsignedValueDecreases — `DataSeries<long, uint>`: 10 at 0, 4 at 1 → `Rate` −6
- test: Should_KeepTheRate_WhenTwoValuesShareTheKey — 0 at 0, 5 at 1 (rate 5), 9 at 1 → `Rate` stays 5, `Last` 9
- test: Should_RejectTheKey_WhenItGoesBack — after a value at key 5, `Add(1, 4)` throws `ArgumentOutOfRangeException` and `Count` stays 1
- test: Should_KeepOnlyTheCapacity_WhenMoreValuesArrive — capacity 3, values 1..5: `StoredCount` 3, `Count` 5, `Get(0)` value 3, `Get(2)` value 5, `Get(3)` throws `ArgumentOutOfRangeException`, `Sum` 15, `Min` 1
- test: Should_SumTheHalfOpenRange_WhenViewSum — `IndexKeys`, values 0..9: `ViewSum(2, 5)` is 9.0
- test: Should_RejectThePoints_WhenLoadedKeysAreNotAscending — `Load` of keys `[1, 3, 2]` throws `ArgumentException`; the series keeps its previous points and `Count`
- test: Should_ReturnTheNewStoredPoints_WhenUpdateSinceIsAsked — 5 values: `UpdateSince(3)` has 2 points (the last two) and `Count` 5; `UpdateSince(5)` has none; after `Clear` and 2 values `UpdateSince(5)` has the 2 stored points; `UpdateSince(-1)` throws `ArgumentOutOfRangeException`
- test: Should_TakeTheIdFromTheName_WhenNoIdIsGiven — `Id` of a series named `"a"` is `HierId.From("a")`; with `HierId.From("g", "a")` it is that id
</red>

### Implementation

`src/Zerox.Core/Statistics/ISeries.cs`:

```csharp
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
```

`src/Zerox.Core/Statistics/DataSeries.cs`:

```csharp
using System.Numerics;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A numeric series: the last `capacity` points, sorted by key, and running statistics over every
// sample since the last Clear. One lock: an add is atomic, a reader never sees half of it.
public sealed class DataSeries<TKey, TValue> : IValueSeries<TValue>
    where TKey : struct, IComparable<TKey>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly ISeriesKeys<TKey> _keys;
    private readonly CircularBuffer<KeyValuePair<TKey, TValue>> _points;
    private readonly IComparer<KeyValuePair<TKey, TValue>> _byKey = KeyComparer.For<TKey, TValue>();
    private long _count;
    private TValue _min;
    private TValue _max;
    private double _sum;
    private double _rate;
    private KeyValuePair<TKey, TValue> _last;

    public DataSeries(string name, ISeriesKeys<TKey> keys, int capacity = 1000, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(keys);
        Name = name;
        Id = id ?? HierId.From(name);
        _keys = keys;
        _points = new(capacity);
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Capacity => _points.Capacity;

    public int StoredCount => _points.Count;

    public long Count { get { lock (_lock) return _count; } }

    public TValue Min { get { lock (_lock) return _min; } }

    public TValue Max { get { lock (_lock) return _max; } }

    public TValue Last { get { lock (_lock) return _last.Value; } }

    public KeyValuePair<TKey, TValue> LastPair { get { lock (_lock) return _last; } }

    public double Sum { get { lock (_lock) return _sum; } }

    public double Mean { get { lock (_lock) return _count == 0 ? 0 : _sum / _count; } }

    // Value change per key distance unit between the last two samples (per second with TimeKeys).
    public double Rate { get { lock (_lock) return _rate; } }

    public void Add(TValue value)
    {
        lock (_lock)
            Append(value, _keys.NextKey());
    }

    public void Add(TValue value, TKey key)
    {
        lock (_lock)
            Append(value, key);
    }

    public KeyValuePair<TKey, TValue>[] View(Interval<TKey> interval)
        => _points.View(ToPoints(interval), _byKey);

    // Sum of the values with from <= key < to.
    public double ViewSum(TKey from, TKey to)
    {
        var sum = 0.0;
        foreach (var point in View(Interval.Range(from, to)))
            sum += double.CreateTruncating(point.Value);
        return sum;
    }

    // The stored point at index (0 = the oldest stored).
    public KeyValuePair<TKey, TValue> Get(int index) => _points[index];

    public KeyValuePair<TKey, TValue>[] Snapshot() => _points.ToArray();

    // Replaces everything with the points (ascending keys); the statistics are recomputed from them.
    public void Load(ReadOnlySpan<KeyValuePair<TKey, TValue>> points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            if (points[i].Key.CompareTo(points[i - 1].Key) < 0)
                throw new ArgumentException($"The keys of the points must be ascending (index {i})", nameof(points));
        }
        lock (_lock)
        {
            ClearUnlocked();
            foreach (var point in points)
                Append(point.Value, point.Key);
        }
    }

    public void Clear()
    {
        lock (_lock)
            ClearUnlocked();
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var points = _points.View(Interval.LastN<KeyValuePair<TKey, TValue>>(SeriesWindow.NewPoints(count, _count, _points.Count)));
            return new(Name, _count, points);
        }
    }

    private void Append(TValue value, TKey key)
    {
        if (_count > 0 && key.CompareTo(_last.Key) < 0)
            throw new ArgumentOutOfRangeException(nameof(key), key, $"The keys of {Name} must not go back (last {_last.Key})");
        var sample = double.CreateTruncating(value);
        if (_count == 0)
        {
            _min = _max = value;
            _rate = 0;
        }
        else
        {
            _min = TValue.Min(_min, value);
            _max = TValue.Max(_max, value);
            var distance = _keys.Distance(_last.Key, key);
            if (distance > 0)
                _rate = (sample - double.CreateTruncating(_last.Value)) / distance;
        }
        _sum += sample;
        _count++;
        _last = new(key, value);
        _points.PushBack(_last);
    }

    private void ClearUnlocked()
    {
        _points.Clear();
        _count = 0;
        _min = _max = TValue.Zero;
        _sum = _rate = 0;
        _last = default;
    }

    private static Interval<KeyValuePair<TKey, TValue>> ToPoints(Interval<TKey> interval)
        => new(interval.From is TKey from ? new(from, default) : null,
               interval.To is TKey to ? new(to, default) : null,
               interval.MaxLastCount);
}
```

Test shape:

```csharp
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Collections;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class DataSeriesTests
{
    private sealed class SteppingKeys(DateTime start, TimeSpan step) : ISeriesKeys<DateTime>
    {
        private DateTime _now = start;

        public DateTime NextKey() => _now += step;

        public double Distance(DateTime earlier, DateTime later) => (later - earlier).TotalSeconds;
    }

    [Fact]
    public void TestRateOfSeries()
    {
        var s = new DataSeries<DateTime, double>("Prova", new SteppingKeys(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1)), 100);
        var rs = new DataSeries<DateTime, double>("ProvaRs", new SteppingKeys(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1)), 100);
        for (var i = 0; i < 100; i += 10)
        {
            s.Add(-i);
            if (i > 0)
            {
                Assert.Equal(-10, s.Rate, 1e-9);
                rs.Add(s.Rate);
            }
        }
        Assert.Equal(9, rs.Count);
        Assert.Equal(-10, rs.Mean, 1e-9);
    }
}
```

<success>
- [ ] `DataSeriesTests` pass (16 tests)
- [ ] `src/Zerox.Core/Statistics/DataSeries.cs` has no `catch` and no `TypeCode` (`grep -n -E "catch|TypeCode" src/Zerox.Core/Statistics/DataSeries.cs` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="04-array-series">
## TDDAB-4: Write the One Row Series and the Counter Widening

<intro>
Delivers `ArraySeries<TKey, TValue>` (same constraints as `DataSeries`): a series of ROWS of
`Width` values (one per channel), the last `capacity` rows stored as
`KeyValuePair<TKey, ImmutableArray<TValue>>`, and per-channel statistics since the last `Clear`:
`Min`, `Max`, `Last` (`ImmutableArray<TValue>`), `Sum`, `Mean`, `Rate` (`ImmutableArray<double>`),
`Count` (`long`, rows), `LastRow`. Before the first row every channel statistic is 0.
- `Add(ReadOnlySpan<TValue> row)` (key from the `ISeriesKeys<TKey>`) and `Add(row, key)`: the row
  length must equal `Width` (`ArgumentException`), the row is COPIED, keys must not go back
  (`ArgumentOutOfRangeException`); per channel the same rules as `DataSeries` (first row: min =
  max = value, rate 0; distance > 0: rate in `double`, a channel that did not change has rate 0);
- `AddParts(params TValue[][] parts)`: the parts joined in order form the row;
- `View(Interval<TKey>)` → rows (copy), `ViewChannel(Interval<TKey>, int channel)` → the points
  of one channel (`ArgumentOutOfRangeException` outside the width), `ViewSum(from, to)` →
  `double[]` per channel over the half-open range, `Get(index)`, `StoredCount`, `Capacity`,
  `Clear`, `UpdateSince(count)` (rows, as `DataSeries`).

And `CounterWidening` (static, same file folder): extends hardware counters that wrap around into
growing `long` values — `Widen(ushort raw, long last)` (a raw value lower than the low 16 bits of
`last` means a wrap: + 0x10000) and `Widen(uint raw, long last)` (32 bits, + 0x1_0000_0000);
`AddCounters(this ArraySeries<TKey, long> series, ReadOnlySpan<ushort> raw)` and the `uint`
overload widen each channel against `series.Last` and add the row (length must equal `Width`).
One producer per series is assumed (it reads `Last`, then adds).

Capability: old `ArrayDataSeriesBase<KT,VT>` (`../../tmp/zerox/Zerox.Wpf.Core/Core/Data/DataSeriesBase.cs`),
`ArrayDateTimeSeries`, `ArrayDoubleDatetimeSeries`, `ArrayUshortDatetimeSeries`,
`ArrayUintDatetimeSeries` (`DoubleDatetimeSeries.cs`), `ArrayLongDatetimeSeriesWithUShortInput.cs`;
tests `ArraySeries` and `TestLongSeriesWithShortInputSeries` in
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/DataTests.cs`. Bugs fixed: the caller's array was
stored (a reused array rewrote the history), a channel that stopped changing kept its old rate, a
row longer than the width overran the statistics arrays. Chart views (`ViewBars`, `View3D`) go to
stage D. Old `ArrayInt32IntSeries` = `ArraySeries<long, int>` with `IndexKeys`;
`ArrayLongDatetimeSeriesWithUShortInput` = `ArraySeries<DateTime, long>` + `AddCounters`.

Files: `src/Zerox.Core/Statistics/ArraySeries.cs`, `src/Zerox.Core/Statistics/CounterWidening.cs`;
tests `tests/Zerox.Core.Tests/Statistics/ArraySeriesTests.cs`,
`tests/Zerox.Core.Tests/Statistics/CounterWideningTests.cs`.
</intro>

<red>
- test: ArraySeries — `ArraySeries<long, int>` width 10 and `DataSeries<long, int>`, rows `i` repeated 10 times and values `i` for `i` in `0..9`: every channel of `ViewSum(0, 10)` equals the `DataSeries` `Sum` (45)
- test: TestLongSeriesWithShortInputSeries — width 10, capacity 100: `AddCounters` of `0xfffe` → no `Last` above `0xffff`; `0xffff` → all `0xffff`; `0x0000` (wrapped) → all `0x10000`; after `Clear`, 0 then 999 times +20 000 (wrapping many times): each `Last` grows by exactly 20 000 and ends at 999 × 20 000
- test: Should_RejectTheRow_WhenItsLengthIsNotTheWidth — width 3: rows of 2 and of 4 values throw `ArgumentException`, `Count` stays 0
- test: Should_CopyTheRow_WhenTheCallerReusesItsArray — add an array, change the array, add it again: the first stored row keeps the first values
- test: Should_SetTheRateToZero_WhenAChannelStopsChanging — keys 0, 1, 2 with channel values 0, 5, 5: after the third row the channel rate is 0 (the old code kept 5)
- test: Should_ComputePerChannelStatistics_WhenRowsArrive — width 2, rows `[1, 10]`, `[3, 20]`, `[2, 30]`: `Min` `[1, 10]`, `Max` `[3, 30]`, `Sum` `[6, 60]`, `Mean` `[2, 20]`, `Last` `[2, 30]`, `Count` 3
- test: Should_JoinTheParts_WhenAddParts — width 4, `AddParts([1, 2], [3, 4])`: `Last` is `[1, 2, 3, 4]`; parts of total length 3 throw `ArgumentException`
- test: Should_ViewOneChannel_WhenViewChannel — rows `[0, 0]`, `[1, 10]`, `[2, 20]` at keys 0, 1, 2: `ViewChannel(Interval.Range(1L, 3L), 1)` is `(1, 10), (2, 20)`; channel 2 throws `ArgumentOutOfRangeException`
- test: Should_ReturnTheNewStoredRows_WhenUpdateSinceIsAsked — 4 rows: `UpdateSince(1)` has 3 rows and `Count` 4
- test: Should_WidenA16BitCounter_WhenItWraps — `Widen((ushort)5, 0x1_FFF0)` is `0x2_0005`; `Widen((ushort)0xFFF5, 0x1_FFF0)` is `0x1_FFF5`
- test: Should_WidenA32BitCounter_WhenItWraps — `Widen(5u, 0x1_FFFF_FFF0)` is `0x2_0000_0005`; `AddCounters` with `uint` raw `[0xFFFF_FFFF]` then `[1]` gives `Last` `[0x1_0000_0001]`
</red>

### Implementation

`src/Zerox.Core/Statistics/ArraySeries.cs`:

```csharp
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A series of rows (one value per channel): the last `capacity` rows and per-channel statistics.
public sealed class ArraySeries<TKey, TValue> : ISeries
    where TKey : struct, IComparable<TKey>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly ISeriesKeys<TKey> _keys;
    private readonly CircularBuffer<KeyValuePair<TKey, ImmutableArray<TValue>>> _rows;
    private readonly IComparer<KeyValuePair<TKey, ImmutableArray<TValue>>> _byKey = KeyComparer.For<TKey, ImmutableArray<TValue>>();
    private readonly TValue[] _min;
    private readonly TValue[] _max;
    private readonly TValue[] _last;
    private readonly double[] _sum;
    private readonly double[] _rate;
    private TKey _lastKey;
    private long _count;

    public ArraySeries(string name, int width, ISeriesKeys<TKey> keys, int capacity = 1000, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentNullException.ThrowIfNull(keys);
        Name = name;
        Id = id ?? HierId.From(name);
        _keys = keys;
        _rows = new(capacity);
        _min = new TValue[width];
        _max = new TValue[width];
        _last = new TValue[width];
        _sum = new double[width];
        _rate = new double[width];
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Width => _last.Length;

    public int Capacity => _rows.Capacity;

    public int StoredCount => _rows.Count;

    public long Count { get { lock (_lock) return _count; } }

    public ImmutableArray<TValue> Min { get { lock (_lock) return [.. _min]; } }

    public ImmutableArray<TValue> Max { get { lock (_lock) return [.. _max]; } }

    public ImmutableArray<TValue> Last { get { lock (_lock) return [.. _last]; } }

    public ImmutableArray<double> Sum { get { lock (_lock) return [.. _sum]; } }

    public ImmutableArray<double> Mean { get { lock (_lock) return [.. _sum.Select(sum => _count == 0 ? 0 : sum / _count)]; } }

    public ImmutableArray<double> Rate { get { lock (_lock) return [.. _rate]; } }

    public KeyValuePair<TKey, ImmutableArray<TValue>> LastRow
    {
        get
        {
            lock (_lock)
                return _count == 0 ? default : new(_lastKey, [.. _last]);
        }
    }

    public void Add(ReadOnlySpan<TValue> row)
    {
        lock (_lock)
            Append(row, _keys.NextKey());
    }

    public void Add(ReadOnlySpan<TValue> row, TKey key)
    {
        lock (_lock)
            Append(row, key);
    }

    // The parts joined in order form the row (several boards read into one row).
    public void AddParts(params TValue[][] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Add(parts.SelectMany(part => part).ToArray());
    }

    public KeyValuePair<TKey, ImmutableArray<TValue>>[] View(Interval<TKey> interval)
        => _rows.View(new(interval.From is TKey from ? new(from, default) : null,
                          interval.To is TKey to ? new(to, default) : null,
                          interval.MaxLastCount), _byKey);

    public KeyValuePair<TKey, TValue>[] ViewChannel(Interval<TKey> interval, int channel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, Width);
        return [.. View(interval).Select(row => new KeyValuePair<TKey, TValue>(row.Key, row.Value[channel]))];
    }

    // Per channel, the sum of the values with from <= key < to.
    public double[] ViewSum(TKey from, TKey to)
    {
        var sums = new double[Width];
        foreach (var row in View(Interval.Range(from, to)))
        {
            for (var i = 0; i < sums.Length; i++)
                sums[i] += double.CreateTruncating(row.Value[i]);
        }
        return sums;
    }

    public KeyValuePair<TKey, ImmutableArray<TValue>> Get(int index) => _rows[index];

    public void Clear()
    {
        lock (_lock)
        {
            _rows.Clear();
            Array.Clear(_min);
            Array.Clear(_max);
            Array.Clear(_last);
            Array.Clear(_sum);
            Array.Clear(_rate);
            _lastKey = default;
            _count = 0;
        }
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var rows = _rows.View(Interval.LastN<KeyValuePair<TKey, ImmutableArray<TValue>>>(SeriesWindow.NewPoints(count, _count, _rows.Count)));
            return new(Name, _count, rows);
        }
    }

    private void Append(ReadOnlySpan<TValue> row, TKey key)
    {
        if (row.Length != Width)
            throw new ArgumentException($"A row of {Name} has {Width} values, not {row.Length}", nameof(row));
        if (_count > 0 && key.CompareTo(_lastKey) < 0)
            throw new ArgumentOutOfRangeException(nameof(key), key, $"The keys of {Name} must not go back (last {_lastKey})");
        var distance = _count == 0 ? 0 : _keys.Distance(_lastKey, key);
        for (var i = 0; i < row.Length; i++)
        {
            var value = row[i];
            var sample = double.CreateTruncating(value);
            if (_count == 0)
            {
                _min[i] = _max[i] = value;
                _rate[i] = 0;
            }
            else
            {
                _min[i] = TValue.Min(_min[i], value);
                _max[i] = TValue.Max(_max[i], value);
                if (distance > 0)
                    _rate[i] = (sample - double.CreateTruncating(_last[i])) / distance;
            }
            _sum[i] += sample;
            _last[i] = value;
        }
        _count++;
        _lastKey = key;
        _rows.PushBack(new(key, ImmutableCollectionsMarshal.AsImmutableArray(row.ToArray())));
    }
}
```

`src/Zerox.Core/Statistics/CounterWidening.cs`:

```csharp
using System.Numerics;

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

    private static System.Collections.Immutable.ImmutableArray<long> LastOf<TKey>(ArraySeries<TKey, long> series, int length)
        where TKey : struct, IComparable<TKey>
    {
        if (length != series.Width)
            throw new ArgumentException($"A row of {series.Name} has {series.Width} values, not {length}", nameof(length));
        return series.Last;
    }
}
```

<success>
- [ ] `ArraySeriesTests` and `CounterWideningTests` pass (11 tests)
- [ ] The row is copied before it is stored (`grep -n "row.ToArray()" src/Zerox.Core/Statistics/ArraySeries.cs` finds the copy in `Append`)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="05-cyclic-series">
## TDDAB-5: Write the One Moving Window Series

<intro>
Delivers `CyclicSeries<TValue>` (`TValue : struct, INumber<TValue>`), an `IValueSeries<TValue>`
over a moving window of the last `size` values (a `CircularBuffer<TValue>` of capacity `size`,
`size` ≥ 1, fixed): `Size`, `WindowCount` (values in the window), `Values` (copy, oldest first),
`Last`, `Sum` and `Mean` of the window (`double`, 0 when empty), `MinMean` / `MaxMean` (the
lowest and highest mean the window has had since the last `Clear`), `Min` / `Max` (the extremes
of the values now in the window, 0 when empty), `Count` (`long`, values added since the last
`Clear`), `Clear`, `UpdateSince(count)` (the stored values added after the first `count`, as a
`TValue[]`), `Name`, `Id` (`HierId.From(name)` when not given). Thread safe: one `Lock`.

Capability: old `CyclicSeries<VT>`, `IntCyclicSeries`, `DoubleCyclicSeries` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Data/` (on a `Queue`), test `CyclicSeries` in
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/DataTests.cs`. The old `Min` / `Max` were the
extremes of the moving mean (the old test pins it): kept and named `MinMean` / `MaxMean` (user
decision); `Min` / `Max` are now the extremes of the window values. Fixed: the int version
truncated the mean into its extremes (now `double`); the old settable `Size` is dropped (a window
cannot be resized consistently). The window reuses this stage's `CircularBuffer<T>` (`PushBack`
returns the value that left the window).

Files: `src/Zerox.Core/Statistics/CyclicSeries.cs`; tests
`tests/Zerox.Core.Tests/Statistics/CyclicSeriesTests.cs`.
</intro>

<red>
- test: CyclicSeries — two `CyclicSeries<int>` of size 50, the first fed `0..199`, the second `150..199`: `(int)(Mean * 100)` equal, `MaxMean` equal, `MinMean` different, `Values.Min()` equal
- test: Should_ForgetTheOldestValue_WhenTheWindowIsFull — size 3, values 1, 2, 3, 10: `Values` `[2, 3, 10]`, `Sum` 15, `Mean` 5, `WindowCount` 3, `Count` 4
- test: Should_ReportTheExtremesOfTheWindow_WhenMinAndMaxAreAsked — size 3, values 9, 1, 5, 6: `Min` 1, `Max` 6; after one more value 7: `Min` 5, `Max` 7
- test: Should_TrackTheExtremesOfTheMean_WhenTheMeanMoves — size 2, values 4, 0, 10: means 4, 2, 5 → `MinMean` 2, `MaxMean` 5
- test: Should_ReportZero_WhenTheSeriesIsEmpty — `Sum`, `Mean`, `Min`, `Max`, `MinMean`, `MaxMean`, `Last`, `Count`, `WindowCount` are 0, `Values` empty; the same after `Clear`
- test: Should_RejectTheSize_WhenItIsLessThanOne — size 0 throws `ArgumentOutOfRangeException`
- test: Should_ReturnTheNewValues_WhenUpdateSinceIsAsked — size 3, values 1..5: `UpdateSince(3)` has `[4, 5]`, `UpdateSince(0)` has `[3, 4, 5]` (only the stored), `Count` 5
</red>

### Implementation

```csharp
using System.Numerics;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A moving window of the last `size` values: sum and mean of the window, the extremes the mean
// has reached (MinMean, MaxMean), the extremes of the values now in the window (Min, Max).
public sealed class CyclicSeries<TValue> : IValueSeries<TValue>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly CircularBuffer<TValue> _window;
    private double _sum;
    private double _minMean;
    private double _maxMean;
    private TValue _last;
    private long _count;

    public CyclicSeries(string name, int size, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Id = id ?? HierId.From(name);
        _window = new(size);
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Size => _window.Capacity;

    public int WindowCount => _window.Count;

    public long Count { get { lock (_lock) return _count; } }

    public TValue[] Values => _window.ToArray();

    public TValue Last { get { lock (_lock) return _last; } }

    public double Sum { get { lock (_lock) return _sum; } }

    public double Mean { get { lock (_lock) return _window.Count == 0 ? 0 : _sum / _window.Count; } }

    public double MinMean { get { lock (_lock) return _minMean; } }

    public double MaxMean { get { lock (_lock) return _maxMean; } }

    public TValue Min
    {
        get
        {
            var values = Values;
            return values.Length == 0 ? TValue.Zero : values.Min();
        }
    }

    public TValue Max
    {
        get
        {
            var values = Values;
            return values.Length == 0 ? TValue.Zero : values.Max();
        }
    }

    public void Add(TValue value)
    {
        lock (_lock)
        {
            if (_window.PushBack(value, out var left))
                _sum -= double.CreateTruncating(left);
            _sum += double.CreateTruncating(value);
            _last = value;
            _count++;
            var mean = _sum / _window.Count;
            _minMean = _count == 1 ? mean : Math.Min(_minMean, mean);
            _maxMean = _count == 1 ? mean : Math.Max(_maxMean, mean);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _window.Clear();
            _sum = _minMean = _maxMean = 0;
            _last = TValue.Zero;
            _count = 0;
        }
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var values = _window.View(Interval.LastN<TValue>(SeriesWindow.NewPoints(count, _count, _window.Count)));
            return new(Name, _count, values);
        }
    }
}
```

<success>
- [ ] `CyclicSeriesTests` pass (7 tests)
- [ ] The window is this stage's buffer, not a `Queue` (`grep -n "Queue<" src/Zerox.Core/Statistics/CyclicSeries.cs` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
