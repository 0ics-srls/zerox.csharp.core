# Aggregation

<block id="06-aggregator">
## TDDAB-6: Port the Series Aggregator and Its Groups

<intro>
Delivers `SeriesAggregator`, the registry of series, and `SeriesGroup`:
- `Register(ISeries)` / `TryRegister(ISeries)`: by `Name` (ordinal); a taken name → `ArgumentException`
  / false; atomic (`ConcurrentDictionary.TryAdd`, of several threads registering one name exactly
  one wins); `Remove(string name)` → bool;
- `Register(SeriesGroup)` / `TryRegister(SeriesGroup)` the same for groups;
- `Add<TValue>(string name, TValue value)`: adds to the `IValueSeries<TValue>` of that name;
  missing name or a series that does not take `TValue` → `ArgumentException`;
- `Get<TSeries>(string name)`: the series as `TSeries`; missing or another type → `ArgumentException`;
- `GetByHierId<TSeries>(Predicate<HierId>)`: every `TSeries` whose `Id` matches, ordered by name;
- `GetSeriesOrGroup<TSeries>(string name)`: the series of that name if it is a `TSeries`,
  otherwise the `TSeries` members of the group of that name, otherwise empty;
- `AvailableNames<TSeries>()`: names of the `TSeries` series and of the groups that contain at
  least one, ordinal order, no duplicates;
- `Series`: every registered series ordered by name.

`SeriesGroup`: a named predicate over series ids, `Contains(HierId)`; `SeriesGroup.Of(name, params
HierId[] members)` (exact ids) and `SeriesGroup.WithPrefix(name, HierId prefix)` (`id.StartsWith(prefix)`).

Capability: old `AbstractDataAgregator`, `IDataAgregator`, `IDataAgregation`, `IDataAgregationTP`,
`ListMatchDataAgregationsGroup`, `PrefixMatchDataAgregationsGroup`, `AbstractDataAgregationsGroup`
in `../../tmp/zerox/Zerox.Wpf.Core/Core/Data/`; tests `MakeSeries`, `MakeSeriesWithGroups`,
`TestSeries` in `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/DataTests.cs`. The old class was
abstract only because the `[Aggregable]` methods were implemented elsewhere: they are block 07's
`AggregableSources` now, so the registry is a plain sealed class (one responsibility). Bugs fixed:
register was check-then-add (two threads could both pass the check); the separate id index kept
only the first series of a given id (`TryAdd` ignored the second) — there is no index now, the
predicate runs on the series' ids. The old series `Tag` (`DataSeriesTag.From("G1").AddOwner(s1).AddOwner(n1)`)
is the series id `HierId.From("G1", "s1", "n1")`; the old `TestSeries` keeps only its aggregator
part (the remote provider has its own block, the timing prints are dropped).

Files: `src/Zerox.Core/Statistics/SeriesAggregator.cs`, `src/Zerox.Core/Statistics/SeriesGroup.cs`;
tests `tests/Zerox.Core.Tests/Statistics/SeriesAggregatorTests.cs`.
</intro>

<red>
- test: MakeSeries — two `DataSeries<DateTime, int>` "SR1", "SR2" register; registering "SR1" or "SR2" again throws `ArgumentException`; `Get<DataSeries<DateTime, int>>` finds both; `Get<DataSeries<long, int>>` of either throws `ArgumentException`
- test: MakeSeriesWithGroups — the old scenario: 5 `DataSeries<DateTime, int>` SR1..SR5, 5 `ArraySeries<long, int>` ASR1..ASR5 (ids `G1/s1/n1`, `G1/s1/n2`, `G2/s1/n3`, `G1/s2/n4`, none), groups `G1.s1` (of `G1/s1/n1`, `G1/s1/n2`), `G2.s1`, `G1.s2`, prefix groups `Pfx-G1.s1` (`G1/s1`), `Pfx-G2.s1`, `Pfx-G1.s2`, `Pfx-G1` (`G1`): `AvailableNames<DataSeries<DateTime, int>>` is `SR1, SR2, SR3, SR4, SR5`; `AvailableNames<ArraySeries<long, int>>` is `ASR1, ASR2, ASR3, ASR4, ASR5, G1.s1, G1.s2, G2.s1, Pfx-G1, Pfx-G1.s1, Pfx-G1.s2, Pfx-G2.s1`; `GetSeriesOrGroup` counts: ASR1 1, G1.s1 2, G2.s1 1, G1.s2 1, Pfx-G1.s1 2, Pfx-G2.s1 1, Pfx-G1.s2 1, Pfx-G1 3
- test: TestSeries — "SR1" fed directly with `0..99`, "SR2" through `aggregator.Add<int>("SR2", i)`: equal `Mean`
- test: Should_ThrowArgument_WhenAddingToAMissingSeries — `Add<int>("none", 1)` throws `ArgumentException`
- test: Should_ThrowArgument_WhenTheValueTypeDoesNotMatch — `Add<double>` to a `DataSeries<long, int>` throws `ArgumentException`; to an `ArraySeries` too
- test: Should_FindEverySeries_WhenTwoShareTheSameId — two series with id `G/x`: the group `Of("g", G/x)` returns both (the old index lost one)
- test: Should_RegisterOnce_WhenManyThreadsRegisterTheSameName — 8 threads `TryRegister` a series named "same": exactly one true
- test: Should_FindSeriesByHierId_WhenThePredicateMatches — ids `a/b`, `a/c`, `d`: `GetByHierId(id => id.StartsWith(HierId.From("a")))` returns the first two, by name
- test: Should_ForgetTheSeries_WhenRemoved — `Remove("SR1")` true, then `Get` throws `ArgumentException`, `Remove("SR1")` false
- test: Should_ReturnNothing_WhenTheNameIsUnknown — `GetSeriesOrGroup<ISeries>("none")` is empty
</red>

### Implementation

`src/Zerox.Core/Statistics/SeriesGroup.cs`:

```csharp
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A named set of series, by their ids: a list of exact ids or an id prefix.
public sealed class SeriesGroup
{
    private readonly Predicate<HierId> _contains;

    private SeriesGroup(string name, Predicate<HierId> contains)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _contains = contains;
    }

    public string Name { get; }

    public bool Contains(HierId id) => _contains(id);

    public static SeriesGroup Of(string name, params HierId[] members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var set = members.ToHashSet();
        return new(name, set.Contains);
    }

    public static SeriesGroup WithPrefix(string name, HierId prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return new(name, id => id.StartsWith(prefix));
    }
}
```

`src/Zerox.Core/Statistics/SeriesAggregator.cs`:

```csharp
using System.Collections.Concurrent;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// The registry of series by name, by id, by group.
public sealed class SeriesAggregator
{
    private readonly ConcurrentDictionary<string, ISeries> _series = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SeriesGroup> _groups = new(StringComparer.Ordinal);

    public IReadOnlyList<ISeries> Series => [.. _series.Values.OrderBy(series => series.Name, StringComparer.Ordinal)];

    public bool TryRegister(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return _series.TryAdd(series.Name, series);
    }

    public void Register(ISeries series)
    {
        if (!TryRegister(series))
            throw new ArgumentException($"A series named {series.Name} is already registered", nameof(series));
    }

    public bool TryRegister(SeriesGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return _groups.TryAdd(group.Name, group);
    }

    public void Register(SeriesGroup group)
    {
        if (!TryRegister(group))
            throw new ArgumentException($"A group named {group.Name} is already registered", nameof(group));
    }

    public bool Remove(string name) => _series.TryRemove(name, out _);

    public void Add<TValue>(string name, TValue value)
    {
        var series = Find(name) as IValueSeries<TValue>
            ?? throw new ArgumentException($"The series {name} does not take {typeof(TValue).Name} values", nameof(value));
        series.Add(value);
    }

    public TSeries Get<TSeries>(string name)
        where TSeries : class, ISeries
        => Find(name) as TSeries
            ?? throw new ArgumentException($"The series {name} is not a {typeof(TSeries).Name}", nameof(name));

    public IReadOnlyList<TSeries> GetByHierId<TSeries>(Predicate<HierId> predicate)
        where TSeries : class, ISeries
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return [.. _series.Values.OfType<TSeries>().Where(series => predicate(series.Id)).OrderBy(series => series.Name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<TSeries> GetSeriesOrGroup<TSeries>(string name)
        where TSeries : class, ISeries
    {
        if (_series.TryGetValue(name, out var series))
            return series is TSeries typed ? [typed] : [];
        return _groups.TryGetValue(name, out var group) ? GetByHierId<TSeries>(group.Contains) : [];
    }

    public IReadOnlyList<string> AvailableNames<TSeries>()
        where TSeries : class, ISeries
        => [.. _series.Values.OfType<TSeries>().Select(series => series.Name)
            .Concat(_groups.Keys.Where(group => GetSeriesOrGroup<TSeries>(group).Count > 0))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private ISeries Find(string name)
        => _series.TryGetValue(name, out var series) ? series : throw new ArgumentException($"No series named {name}", nameof(name));
}
```

<success>
- [ ] `SeriesAggregatorTests` pass (10 tests)
- [ ] No id index beside the series (`grep -n "HierIndex\|ConcurrentDictionary<HierId" src/Zerox.Core/Statistics/SeriesAggregator.cs` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="07-aggregable-sources">
## TDDAB-7: Write the Declarative Series of Services

<intro>
Delivers the declarative side: a service marks its measurable properties and the system creates
and feeds their series.
- `AggregableAttribute` (properties, inherited): `Name` (series name part; the property name when
  null), `Group` (a group the series joins), `Color` (`#RRGGBB` or `#RRGGBBAA`, read by the charts
  of stage D);
- `IAggregableSource : INotifyPropertyChanged` with `string Name`: what a service implements;
- `AggregableSources(SeriesAggregator aggregator, TimeProvider timeProvider, int capacity = 1000)`:
  - `Register(IAggregableSource source)` → the created series: for every `[Aggregable]` property
    (stage A's `GetPropertiesWith<AggregableAttribute>()`) a `DataSeries<DateTime, double>` named
    `"<source.Name>.<attribute.Name ?? property.Name>"`, id `HierId.From(group, source.Name, name)`
    when `Group` is set (and the prefix group `SeriesGroup.WithPrefix(group, HierId.From(group))`
    is registered if absent), `HierId.From(source.Name, name)` otherwise, keys `TimeKeys` on the
    `TimeProvider`, values read through `FastProperty.For<object, double>(property)` (stage A);
    a marked property that is not numeric (`byte, sbyte, short, ushort, int, uint, long, ulong,
    float, double, decimal`) or has no public getter → `ArgumentException` and nothing is
    registered; a source registered twice or a series name already taken → `ArgumentException`
    and nothing of this source stays registered;
  - a `PropertyChanged` of a marked property (or with an empty/null name: all of them) samples
    that property into its series;
  - `Sample()`: samples every marked property of every registered source (the old
    `ForceUpdateAgregables`); a getter that raises its own `PropertyChanged` while being sampled
    (the old `SystemResourcesMonitor` refreshed in its getters) is sampled once, not twice;
  - `Unregister(source)` → bool: stops listening and removes the source's series from the
    aggregator;
  - an exception of a getter reaches the caller of `Sample()` or the raiser of `PropertyChanged` (P9).

Capability: old `AgregableAttribute`, `IAgregablesContainer`, the abstract
`CheckAndInitForAgregableObject` / `ForceUpdateAgregables` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Data/` — the implementation is NOT in the copy; the contract
comes from its user `../../tmp/zerox/Zerox.Wpf.Core/Core/Wpf/SystemResourcesMonitor.cs`
(`[Agregable(AgregablesGroup = "SysInfo", Name = "Used ram", Color = …)]` on properties whose
getters refresh the values). No old tests. A separate class from the registry (one
responsibility each: `SeriesAggregator` keeps series, `AggregableSources` discovers and samples).

Files: `src/Zerox.Core/Statistics/AggregableAttribute.cs`, `src/Zerox.Core/Statistics/IAggregableSource.cs`,
`src/Zerox.Core/Statistics/AggregableSources.cs`; tests
`tests/Zerox.Core.Tests/Statistics/AggregableSourcesTests.cs`.
</intro>

<red>
- test: Should_CreateOneSeriesPerMarkedProperty_WhenASourceIsRegistered — a source "Monitor" with `[Aggregable(Group = "SysInfo", Name = "Used ram")] double UsedRam`, `[Aggregable] int Threads`, unmarked `double Other`: two series, "Monitor.Used ram" (id `SysInfo/Monitor/Used ram`) and "Monitor.Threads" (id `Monitor/Threads`), both in the aggregator
- test: Should_RegisterThePrefixGroup_WhenAPropertyNamesAGroup — after registering, `GetSeriesOrGroup<ISeries>("SysInfo")` returns "Monitor.Used ram"; a second source with the same group registers without error and the group returns both
- test: Should_AddTheValue_WhenTheSourceRaisesPropertyChanged — setting `UsedRam = 42` (raises `PropertyChanged`) adds 42 to "Monitor.Used ram" only; the key is the fake clock's time
- test: Should_SampleEveryProperty_WhenAnEmptyNameIsRaised — `PropertyChanged` with `null` name adds one sample to each series
- test: Should_SampleEveryProperty_WhenSampleIsCalled — two sources: `Sample()` adds one value to each of their series, values read from the getters
- test: Should_SampleOnce_WhenTheGetterRaisesPropertyChangedDuringSample — a getter that sets its backing field and raises `PropertyChanged` for itself: `Sample()` leaves `Count` 1
- test: Should_RejectTheSource_WhenAMarkedPropertyIsNotNumeric — `[Aggregable] string Label` → `ArgumentException`, no series registered
- test: Should_RejectTheSource_WhenItIsRegisteredTwice — the second `Register` throws `ArgumentException` and the first registration still works
- test: Should_RollBack_WhenASeriesNameIsTaken — the aggregator already has "Monitor.Threads": `Register` throws `ArgumentException` and "Monitor.Used ram" is not left registered
- test: Should_StopListening_WhenTheSourceIsUnregistered — `Unregister` true, the series are gone from the aggregator, a later `PropertyChanged` adds nothing; `Unregister` again false
- test: Should_LetTheGetterExceptionReachTheCaller_WhenSampling — a getter throwing `InvalidOperationException`: `Sample()` throws it
</red>

### Implementation

`src/Zerox.Core/Statistics/AggregableAttribute.cs`:

```csharp
namespace Zerox.Core.Statistics;

// Marks a numeric property of an IAggregableSource as a series the system creates and feeds.
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class AggregableAttribute : Attribute
{
    // The series name part; the property name when null.
    public string? Name { get; set; }

    // A group the series joins (a prefix group of that name is registered).
    public string? Group { get; set; }

    // #RRGGBB or #RRGGBBAA, read by the charts.
    public string? Color { get; set; }
}
```

`src/Zerox.Core/Statistics/IAggregableSource.cs`:

```csharp
using System.ComponentModel;

namespace Zerox.Core.Statistics;

// A service whose [Aggregable] properties become series; a property change samples it.
public interface IAggregableSource : INotifyPropertyChanged
{
    string Name { get; }
}
```

`src/Zerox.Core/Statistics/AggregableSources.cs`:

```csharp
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Zerox.Core.Identity;
using Zerox.Core.Reflection;

namespace Zerox.Core.Statistics;

// Discovers the [Aggregable] properties of services, creates their series in the aggregator and samples them.
public sealed class AggregableSources(SeriesAggregator aggregator, TimeProvider timeProvider, int capacity = 1000)
{
    private static readonly HashSet<Type> Numeric =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    ];

    private readonly SeriesAggregator _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
    private readonly TimeProvider _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ConcurrentDictionary<IAggregableSource, Source> _sources = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<DataSeries<DateTime, double>> Register(IAggregableSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var probes = source.GetType().GetPropertiesWith<AggregableAttribute>()
            .Select(marked => CreateProbe(source, marked.Property, marked.Attribute))
            .ToArray();
        var entry = new Source(source, probes);
        if (!_sources.TryAdd(source, entry))
            throw new ArgumentException($"The source {source.Name} is already registered", nameof(source));
        for (var i = 0; i < probes.Length; i++)
        {
            if (!_aggregator.TryRegister(probes[i].Series))
            {
                foreach (var registered in probes.Take(i))
                    _aggregator.Remove(registered.Series.Name);
                _sources.TryRemove(source, out _);
                throw new ArgumentException($"A series named {probes[i].Series.Name} is already registered", nameof(source));
            }
            if (probes[i].Group is { } group)
                _aggregator.TryRegister(SeriesGroup.WithPrefix(group, HierId.From(group)));
        }
        source.PropertyChanged += entry.OnPropertyChanged;
        return [.. probes.Select(probe => probe.Series)];
    }

    public bool Unregister(IAggregableSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!_sources.TryRemove(source, out var entry))
            return false;
        source.PropertyChanged -= entry.OnPropertyChanged;
        foreach (var probe in entry.Probes)
            _aggregator.Remove(probe.Series.Name);
        return true;
    }

    // Samples every marked property of every registered source.
    public void Sample()
    {
        foreach (var entry in _sources.Values)
            entry.SampleAll();
    }

    private Probe CreateProbe(IAggregableSource source, PropertyInfo property, AggregableAttribute attribute)
    {
        if (!Numeric.Contains(property.PropertyType))
            throw new ArgumentException($"{source.Name}.{property.Name} is [Aggregable] but not numeric", nameof(source));
        var getter = FastProperty.For<object, double>(property);
        if (!getter.CanRead)
            throw new ArgumentException($"{source.Name}.{property.Name} is [Aggregable] but has no public getter", nameof(source));
        var name = attribute.Name ?? property.Name;
        var id = attribute.Group is { } group ? HierId.From(group, source.Name, name) : HierId.From(source.Name, name);
        var series = new DataSeries<DateTime, double>($"{source.Name}.{name}", new TimeKeys(_time), capacity, id);
        return new(property.Name, attribute.Group, getter, series);
    }

    private sealed record Probe(string Property, string? Group, FastProperty<object, double> Getter, DataSeries<DateTime, double> Series)
    {
        public void Sample(object owner) => Series.Add(Getter.Get(owner));
    }

    private sealed class Source(IAggregableSource owner, Probe[] probes)
    {
        // The source being sampled on this thread: its own PropertyChanged raised by a getter is not sampled again.
        [ThreadStatic]
        private static Source? _sampling;

        public Probe[] Probes => probes;

        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_sampling == this)
                return;
            foreach (var probe in probes)
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == probe.Property)
                    probe.Sample(owner);
            }
        }

        public void SampleAll()
        {
            var outer = _sampling;
            _sampling = this;
            try
            {
                foreach (var probe in probes)
                    probe.Sample(owner);
            }
            finally
            {
                _sampling = outer;
            }
        }
    }
}
```

The `[ThreadStatic]` field name follows the repository's naming rules (`.editorconfig`); if the
analyzers ask for another prefix, rename it, nothing else.

<success>
- [ ] `AggregableSourcesTests` pass (11 tests)
- [ ] The discovery and the sampling reuse stage A (`grep -n -E "GetPropertiesWith|FastProperty.For" src/Zerox.Core/Statistics/AggregableSources.cs` finds both; no `GetCustomAttribute` / `GetValue(` there)
- [ ] No `catch` in `src/Zerox.Core/Statistics/AggregableSources.cs`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="08-remote-data-provider">
## TDDAB-8: Write the Basic Remote Data Provider

<intro>
Delivers `RemoteDataProvider` (`IAsyncDisposable`): periodically sends the NEW points of chosen
series (or groups) to a remote consumer through a channel. A basic implementation, enough to be
tested (user decision: the concrete old provider is not in the copy).
- `RemoteDataProvider(SeriesAggregator aggregator, TimeProvider timeProvider, TimeSpan period)`;
  `period` ≤ 0 → `ArgumentOutOfRangeException`; `RemoteDataProvider.DefaultPeriod` = 250 ms (the
  old sender's period);
- `Updates`: `ChannelReader<SeriesUpdate>` (unbounded, one writer); the consumer reads it;
- `AvailableSeries()`: `aggregator.AvailableNames<ISeries>()`;
- `Start(IEnumerable<string> names)`: each name must give at least one series through
  `GetSeriesOrGroup<ISeries>` (`ArgumentException` otherwise); already started →
  `InvalidOperationException`; the `PeriodicTimer(period, timeProvider)` is created inside
  `Start` (a fake clock advanced right after `Start` already ticks it); the loop runs through stage
  A's `OwnerQueue.StartDetached` (an exception escaping it stops the process, P9); at each tick,
  for every series of every name, `UpdateSince(sent count)` and, when it has points, one
  `SeriesUpdate` written to `Updates`; the sent count is remembered per series name (a series in
  two names is sent once);
- `StopAsync()`: stops the loop and waits for its end (disposing the timer makes the pending wait
  return false: no exception is used to stop); safe when not started; `Start` again after a stop
  is allowed;
- `DisposeAsync()`: `StopAsync()`, then completes `Updates`.

Capability: old `IRemoteDataProvider`, `RemoteDataProviderBase` (a `ConcurrentQueue` flushed to
a weak event every 250 ms by a loop on the old cancellation context), `SeriesUpdateDataMessage`,
`AvailableSeriesDataMessage` in `../../tmp/zerox/Zerox.Wpf.Core/Core/Data/AbstractDataAgregator.cs`;
the concrete crawler `HalRemoteDataProvider` is not in the copy and its old test `TestSeries` only
printed. The weak event becomes a channel (the consumer pulls, nothing is lost when no handler is
attached); the queue and the sender loop become the timer loop.

Files: `src/Zerox.Core/Statistics/RemoteDataProvider.cs`; tests
`tests/Zerox.Core.Tests/Statistics/RemoteDataProviderTests.cs` (read `Updates` with
`ReadAsync(TestContext.Current.CancellationToken)`; the fake clock is advanced after the points are
added; no sleep).
</intro>

<red>
- test: Should_SendTheNewPoints_WhenThePeriodElapses — series "a" (`DataSeries<long, int>`, `IndexKeys`), `Start(["a"])`, 3 values added, `Advance(250 ms)`: the update read has `SeriesName` "a", 3 points, `Count` 3
- test: Should_SendOnlyWhatIsNew_WhenTheNextPeriodElapses — after the first update was read, 2 more values and `Advance(250 ms)`: the next update has the 2 new points and `Count` 5
- test: Should_CrawlEverySeriesOfAGroup_WhenTheNameIsAGroup — group "g" of "a" and "b", one value each, `Start(["g"])`, `Advance`: two updates, one per series
- test: Should_SendASeriesOnce_WhenTwoNamesContainIt — `Start(["a", "g"])` with "a" in "g": one update for "a" per tick
- test: Should_RejectTheName_WhenNoSeriesOrGroupHasIt — `Start(["none"])` throws `ArgumentException`
- test: Should_ThrowInvalidOperation_WhenStartedTwice — the second `Start` throws `InvalidOperationException`
- test: Should_StopSending_WhenStopped — `StopAsync()` completes; values added and `Advance(1 s)`: `Updates.TryRead` is false; `Start` again works
- test: Should_ListTheAvailableSeries_WhenAsked — series "a", "b" and group "g": `AvailableSeries()` is `a, b, g`
- test: Should_RejectThePeriod_WhenItIsNotPositive — `TimeSpan.Zero` throws `ArgumentOutOfRangeException`
- test: Should_CompleteTheUpdates_WhenDisposed — after `DisposeAsync()`, `Updates.Completion` is completed
</red>

### Implementation

```csharp
using System.Threading.Channels;
using Zerox.Core.Concurrency;

namespace Zerox.Core.Statistics;

// Periodically sends the new points of chosen series (or groups) through the Updates channel.
public sealed class RemoteDataProvider : IAsyncDisposable
{
    private readonly SeriesAggregator _aggregator;
    private readonly TimeProvider _time;
    private readonly TimeSpan _period;
    private readonly Channel<SeriesUpdate> _updates = Channel.CreateUnbounded<SeriesUpdate>(new() { SingleWriter = true });
    private readonly Lock _lock = new();
    private PeriodicTimer? _timer;
    private Task _stopped = Task.CompletedTask;

    public RemoteDataProvider(SeriesAggregator aggregator, TimeProvider timeProvider, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _aggregator = aggregator;
        _time = timeProvider;
        _period = period;
    }

    public static TimeSpan DefaultPeriod { get; } = TimeSpan.FromMilliseconds(250);

    public ChannelReader<SeriesUpdate> Updates => _updates.Reader;

    public IReadOnlyList<string> AvailableSeries() => _aggregator.AvailableNames<ISeries>();

    public void Start(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        string[] crawled = [.. names];
        foreach (var name in crawled)
        {
            if (_aggregator.GetSeriesOrGroup<ISeries>(name).Count == 0)
                throw new ArgumentException($"No series or group named {name}", nameof(names));
        }
        lock (_lock)
        {
            if (_timer is not null)
                throw new InvalidOperationException("The provider is already crawling: stop it first");
            var timer = new PeriodicTimer(_period, _time);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _timer = timer;
            _stopped = stopped.Task;
            OwnerQueue.StartDetached(() => CrawlAsync(timer, crawled, stopped));
        }
    }

    public async Task StopAsync()
    {
        PeriodicTimer? timer;
        Task stopped;
        lock (_lock)
        {
            timer = _timer;
            stopped = _stopped;
            _timer = null;
        }
        if (timer is null)
            return;
        timer.Dispose();   // the pending wait returns false: the loop ends
        await stopped.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _updates.Writer.TryComplete();
    }

    private async Task CrawlAsync(PeriodicTimer timer, string[] names, TaskCompletionSource stopped)
    {
        try
        {
            var sent = new Dictionary<string, long>(StringComparer.Ordinal);
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
                Collect(names, sent);
        }
        finally
        {
            stopped.SetResult();
        }
    }

    private void Collect(string[] names, Dictionary<string, long> sent)
    {
        var tick = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            foreach (var series in _aggregator.GetSeriesOrGroup<ISeries>(name))
            {
                if (!tick.Add(series.Name))
                    continue;
                var update = series.UpdateSince(sent.GetValueOrDefault(series.Name));
                sent[series.Name] = update.Count;
                if (update.Points.Length > 0)
                    _updates.Writer.TryWrite(update);
            }
        }
    }
}
```

<success>
- [ ] `RemoteDataProviderTests` pass (10 tests), no `Thread.Sleep` / `Task.Delay` in them
- [ ] The loop is started by `OwnerQueue.StartDetached` and has no `catch` (`grep -n "catch" src/Zerox.Core/Statistics/RemoteDataProvider.cs` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
