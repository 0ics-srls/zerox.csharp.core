# 03-port-statistics — notes

## Requirement

Stage B of the port of the old Zerox code (stage A, `02-port-foundations`, is done): the realtime
statistics stack. The source is the old code at `assetforge/tmp/zerox` (outside this repository,
never edited): `Zerox.Wpf.Core/Core/Data/*`, `Zerox.Wpf.Core/Core/Collections/IInterval.cs`, the
tests `Zerox.Wpf.XTest.Core/Core/Data/CircularBufferTest.cs` and `DataTests.cs`, and the only
user of `[Agregable]` in the copy, `Zerox.Wpf.Core/Core/Wpf/SystemResourcesMonitor.cs`.

What the stack does: a producer adds samples at a high rate (also on weak CPUs); each series keeps
the last N points in a circular buffer with running statistics (min, max, sum, mean, rate, last);
views over a key interval return the points of a time window or the last N; an aggregator holds
the series by name and by hierarchical id, with groups; services declare their measurable
properties with `[Aggregable]` and the system creates and feeds their series; a remote data
provider periodically sends the new points of chosen series to a remote consumer.

Decisions taken with the user:
- the concrete remote provider of the old code (`HalRemoteDataProvider`) is not in the copy (it
  lived in another product); a basic implementation is written, enough to be tested;
- the old cyclic series reported as `Min` / `Max` the extremes reached by the moving MEAN (the old
  test pins it): the behaviour is kept and the properties are named `MinMean` / `MaxMean`; `Min`
  / `Max` become the extremes of the values in the window.

"Port" means what it meant in stage A: the capability and its tests come over, the code is
rewritten where the old one breaks a rule of this repository, every bug fixed on the way is
pinned by a test. Ported tests keep their names; new tests are `Should_<Outcome>_When<Condition>`.

## Code Organization

1. **What is shared.** `Interval<T>` and `CircularBuffer<T>` are general collections (any sorted
   data with a window), so they live in `Zerox.Core.Collections`; the series, the aggregator, the
   `[Aggregable]` discovery and the remote provider live in a new area `Zerox.Core.Statistics`.
   Consumed later by stage D (charts in Avalonia) and by assetforge.
2. **Who owns it.** `src/Zerox.Core/Collections` (interval, circular buffer, key comparer),
   `src/Zerox.Core/Statistics` (everything else). Tests mirror the folders.
3. **What is reused from stage A.** `SortedSearch.LowerBound` (the views of the circular buffer),
   `HierId` (the id of a series, the groups), `AttributeExtensions.GetPropertiesWith` and
   `FastProperty` (the `[Aggregable]` discovery and the sampling), `OwnerQueue.StartDetached`
   (the crawling loop of the remote provider: an exception escaping it stops the process, P9).
   Nothing of these is written again.
4. **Contracts.** Interfaces only where a consumer holds several implementations:
   - `ISeriesKeys<TKey>`: where the key of a new sample comes from and how far two keys are
     (`TimeKeys` on a `TimeProvider`, `IndexKeys` a counter); the series consume it;
   - `ISeries` / `IValueSeries<TValue>`: what the aggregator and the remote provider see of a
     series (`DataSeries`, `ArraySeries`, `CyclicSeries`);
   - `IAggregableSource`: what a service implements to be discovered (name + property changes).

   One generic series per shape instead of one class per value type: `DataSeries<TKey, TValue>`
   and `ArraySeries<TKey, TValue>` with `TValue : INumber<TValue>` (generic math replaces the
   old `switch` on `TypeCode`, the `NumbersHelper` expressions and the eight copies of
   `UpdateInternal`).
5. **Mechanism.** Each series owns one `Lock`: an add (key, statistics, buffer) is atomic and a
   reader never sees half an update. A view is a copy taken under the buffer's lock (the old view
   was lazy and kept a pooled interval already given back to the pool). Time comes from a
   `TimeProvider` (keys, the provider's period): tests use `FakeTimeProvider` and never sleep.
   Persistence is the consumer's: a series gives a `Snapshot()` and takes a `Load(points)`.
6. **What this stage does not own.** Charts and colors (`ChartSeriesSource`, `IChartDataProvider`,
   brushes, `DataSeriesTag` colors, `MixClassPalette`, `HistCurveSegments`, bar/3D views) → stage
   D. Change notifications of a series are added by stage D with the chart that consumes them
   (no consumer here).

## Inventario — capacità e dove esistono già

Searched: the old code (every file of `Core/Data`, `Collections/IInterval.cs`, the two test files,
`SystemResourcesMonitor`), this repository after stage A (`Collections`, `Identity`, `Reflection`,
`Concurrency`), the .NET 10 base library.

| Capability (what it does) | Old code | Here / .NET 10 | Decision |
|---|---|---|---|
| Window over a sorted sequence: from (inclusive), to (exclusive), then only the last N | `Collections/IInterval.cs`: `IInterval<KT>`, `DateTimeInterval`, `GenericKVSeriesInterval`, `DoubleDateTimeSeriesInterval`, `DoubleIntSeriesInterval`, `OnlyCountSeriesInterval` (six types, object pools, `DateTime.Now`, a static test hook) | — | ONE `readonly record struct Interval<T>(T? From, T? To, int? MaxLastCount)` + factories `Range`, `Since`, `LastN`, `Last(span, now)`; no pool (a struct allocates nothing), `now` passed by the caller |
| Fixed-capacity ring with push/pop at both ends, the overwritten item returned, views by interval | `Data/CircularBuffer.cs` (+ `CircularBufferTest`, 7 tests) | `SortedSearch.LowerBound` (stage A) for the bounds | port as `Collections/CircularBuffer<T>`; fixes: the lazy view kept a pooled interval already returned to the pool (now a copy), `IndexOutOfRangeException` → `ArgumentOutOfRangeException`, `PopBack`/`PopFront` checked emptiness outside the lock and now return the item |
| Compare key/value pairs by key | `GenericKVSeriesInterval.Compare` | — | `Collections/KeyComparer.For<TKey, TValue>()` |
| Key of the next sample and distance between keys | `ISeriesKeyProvider<KT>`, `DateTimeKeyProvider` (`DateTime.Now`), `GetKeyDelta` copied in every series (ms, 0 → 1) | `TimeProvider` | `ISeriesKeys<TKey>` + `TimeKeys` (monotonic, on `TimeProvider`, distance in seconds) + `IndexKeys` (counter, distance in steps) |
| Series of numbers with last N points, min, max, sum, mean, rate, last, views, sum of a range | `DataSeriesBase<KT,VT>` + `DateTimeSeries<VT>`, `DoubleDatetimeSeries`, `Int32DatetimeSeries` (+ tests `TestRateOfSeries`, `TestSeriesView`, persistence tests) | generic math `INumber<T>` | ONE `DataSeries<TKey, TValue>`; fixes: the rate of an integer series was an integer division (lost its fraction), the first rate was computed against a default key, a key going back broke the sorted buffer silently (now `ArgumentOutOfRangeException`), `Get(index)` copied the whole buffer, a failed buffer add was written to `Trace` and ignored (P9); `Medium` → `Mean`; `ViewSum` returns `double` (the old `long` truncated doubles) |
| Persist and reload a series | `IDataSeriesPersistor`, `DictionaryDataSeriesPersistor`, Mongo persistor, `DataSeriesData` | — | `Snapshot()` / `Load(points)` on the series; the consumer stores the points where it wants (the in-memory dictionary becomes test code, Mongo is dropped) |
| Series of rows (one value per channel) with per-channel statistics, a channel cut, a range sum | `ArrayDataSeriesBase<KT,VT>` + `ArrayDateTimeSeries`, `ArrayDoubleDatetimeSeries`, `ArrayUshortDatetimeSeries`, `ArrayUintDatetimeSeries` (+ test `ArraySeries`) | — | ONE `ArraySeries<TKey, TValue>`; fixes: the caller's array was stored (reusing it rewrote the history; now copied), the rate of a channel that stopped changing kept its old value (now 0), a row longer than the width read past the arrays (now `ArgumentException`, length must equal the width); `Add(params VT[][])` kept as `AddParts` |
| 16/32-bit hardware counters extended to `long` across wrap-arounds | `ArrayLongDatetimeSeriesWithUShortInput` (+ `TestLongSeriesWithShortInputSeries`) | — | `CounterWidening.Widen` + `AddCounters` extensions on `ArraySeries<TKey, long>` |
| Moving window of the last N values: sum, mean, extremes of the mean | `CyclicSeries<VT>`, `IntCyclicSeries`, `DoubleCyclicSeries` (+ test `CyclicSeries`) | `CircularBuffer<T>` (this stage) instead of the old `Queue` | ONE `CyclicSeries<TValue>`; `Min`/`Max` of the mean → `MinMean`/`MaxMean` (user decision), new `Min`/`Max` of the window values; the int version truncated the mean into its extremes (now `double`); the settable `Size` dropped (the window cannot be resized consistently) |
| Registry of series by name, by hierarchical id, by group; typed lookup; available names | `AbstractDataAgregator`, `IDataAgregator`, `IDataAgregation*` (+ tests `MakeSeries`, `MakeSeriesWithGroups`, `TestSeries`) | `ConcurrentDictionary` | `SeriesAggregator`; fixes: register was check-then-add (two threads could both pass), two series with the same id lost one in the id index (the index is gone: the predicate runs on the series' ids) |
| Groups by list of ids or by id prefix | `ListMatchDataAgregationsGroup`, `PrefixMatchDataAgregationsGroup`, `AbstractDataAgregationsGroup` | `HierId.StartsWith` (stage A) | ONE `SeriesGroup` with `Of(name, ids)` and `WithPrefix(name, prefix)` |
| Declarative series: a service marks properties `[Aggregable]`, the system creates their series and samples them | `AgregableAttribute`, `IAgregablesContainer`, `CheckAndInitForAgregableObject` (abstract: the implementation is not in the copy), `ForceUpdateAgregables` | `GetPropertiesWith`, `FastProperty` (stage A) | write `AggregableAttribute`, `IAggregableSource`, `AggregableSources` (register, unregister, sample all; a sample on a property change; a getter that raises its own change while being sampled is sampled once) |
| Send the new points of chosen series to a remote consumer, periodically | `IRemoteDataProvider`, `RemoteDataProviderBase` (queue + weak event every 250 ms), `SeriesUpdateDataMessage`, `AvailableSeriesDataMessage`; concrete `HalRemoteDataProvider` absent | `PeriodicTimer` on `TimeProvider`, `Channel<T>` | basic `RemoteDataProvider` (user decision): `Start(names)`, `StopAsync`, `Updates` channel of `SeriesUpdate`, `AvailableSeries()` |
| Enumerator holding a lock while enumerating | `Data/SafeEnumerator.cs` | — | drop (views are copies) |
| Arithmetic between two numeric types through compiled expressions | `Data/NumbersHelper.cs` (its `Div` divided a value by itself) | generic math | drop |
| Design-time aggregator for the WPF designer | `DesignTimeDataAgregator` | — | drop (WPF) |
| Series converters for two products (`FENIX`, `REMOTE`) | `IDataSeriesConverter`, `DataSeriesConversionKind` | — | drop (product code) |
| Charts: series source, colors, palettes, histogram curves, bar and 3D views, chart activation | `ChartSeriesSource`, `IChartDataSource*`, `IChartDataProvider`, `DataSeriesTag` colors, `MixClassPalette`, `HistCurveSegments`, `ViewBars`, `View3D`, `ActivateForChart` | — | stage D (Avalonia) |
| `ForEach`, parallel for-each, reverse | `Collections/EnumerableExtensions.cs` | `Parallel.ForEachAsync`, `Task.WhenAll`, `Enumerable.Reverse` | drop (the parallel ones blocked threads with `RunSync` / `WaitAll`) |
| Set of devices with a presence watchdog | `Collections/TemporarySetWitMaxTimeOfPresence.cs` (no tests) | — | drop (product code of the P80 machines, `IHasName`/`IHasGuid`, recursive watch) |
