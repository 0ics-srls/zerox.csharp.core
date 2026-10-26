# TDDAB Plan: port the realtime statistics of the old Zerox code
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Its root is the repository root (written `.`
below); every path in this plan is relative to it. It is checked out as the submodule
`externals/zerox.csharp.core` of assetforge; run every command from the repository root and
commit inside this repository (branch `feature/03-port-statistics`), never in assetforge, never
push.

This plan is stage B of the port of the old Zerox code: the realtime statistics stack (circular
buffer with interval views, numeric series with running statistics, an aggregator of series with
groups, declarative `[Aggregable]` series of services, a basic remote data provider). The old
code is a read-only copy at `../../tmp/zerox` (the assetforge checkout that holds this
submodule): `../../tmp/zerox/Zerox.Wpf.Core/Core/Data`, `../../tmp/zerox/Zerox.Wpf.Core/Core/Collections/IInterval.cs`,
the tests `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/CircularBufferTest.cs` and
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/DataTests.cs` (NUnit). Never edit it; read it when
a block says so. "Port" means: the capability and its tests come over, the code is rewritten
where the old one breaks a rule below, and every bug fixed on the way is pinned by a test.

Stage A (`tasks/02-port-foundations`) is done and provides, in `src/Zerox.Core`:
- `Collections/SortedSearch.cs` :: `SortedSearch.LowerBound<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)` (index of the first element not less than value);
- `Identity/HierId.cs` :: `HierId` (immutable hierarchical id `a/b/c`): `HierId.From(params string[] parts)`, `id.StartsWith(HierId prefix)`, value equality and hash;
- `Reflection/AttributeExtensions.cs` :: `type.GetPropertiesWith<TAttribute>()` → `(PropertyInfo Property, TAttribute Attribute)` of every public property carrying the attribute;
- `Reflection/FastProperty.cs` :: `FastProperty.For<TInstance, TValue>(PropertyInfo property, bool publicOnly = true)` → compiled accessor with `Get(instance)`, `CanRead` (a value type converts to `TValue`);
- `Concurrency/OwnerQueue.cs` :: `OwnerQueue.StartDetached(Func<Task> work)` (starts work outside the caller's flow; an exception escaping it stops the process).

New area of this stage: `src/Zerox.Core/Statistics` (namespace `Zerox.Core.Statistics`), tests in
`tests/Zerox.Core.Tests/Statistics`. Test projects get xunit v3, AwesomeAssertions,
`using Xunit` and `using AwesomeAssertions` from `Directory.Build.targets`; `tests/Zerox.Core.Tests`
already references `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`, namespace
`Microsoft.Extensions.Time.Testing`). Production internals are visible to the matching `*.Tests`
project.

## Stack (locked)

- .NET SDK 10.0.400, `net10.0`, C# 14 (first-class span conversions: an array argument binds to a `ReadOnlySpan<T>` parameter), Nullable, ImplicitUsings.
- Central Package Management in `Directory.Packages.props`; no version in a `.csproj`. Nothing is added: `System.Threading.Channels`, `System.Collections.Immutable` and `PeriodicTimer` are part of the framework.
- Tests: xunit v3 (Microsoft Testing Platform) + AwesomeAssertions. Ported tests keep their old names and may keep xunit `Assert`; new tests are named `Should_<Outcome>_When<Condition>` and use AwesomeAssertions. Timing-dependent tests (real threads racing through long loops) carry `[Trait("Category", "Integration")]`.
- Time: whatever reads the clock or waits takes a `TimeProvider` in its constructor; tests use `FakeTimeProvider` and never sleep.
- Generic math: numeric series take `TValue : struct, INumber<TValue>`; statistics in `double` through `double.CreateTruncating(value)`.

## Rules

- Everything English: identifiers, comments, strings, test names of new tests, commit messages.
- Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in `src`). Library code awaits with `.ConfigureAwait(false)`. No static members on generic types (CA1000): factories live on a non-generic static class. Library code never throws `IndexOutOfRangeException`, `ApplicationException`, `Exception` (CA2201): misuse is `ArgumentException` / `ArgumentOutOfRangeException` / `ArgumentNullException`, a wrong state is `InvalidOperationException`.
- P9, no exception is hidden: no `catch` that goes on without rethrowing; an exception nobody expected reaches its caller, or, where there is no caller, the process.
- KISS, DRY, SOLID: an interface only where a consumer holds several implementations (`ISeriesKeys<TKey>`, `ISeries`, `IValueSeries<TValue>`, `IAggregableSource`); one implementation per capability (see Shared Capabilities); constructor injection, no container, no static service locator; one `Lock` per series.
- Public types are the library API (`public sealed` unless a block says otherwise); helpers are `internal`.
- Commit per block, staging files by path. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Core.Tests` (filter one class: `dotnet test --project tests/Zerox.Core.Tests -- --filter-class "Zerox.Core.Tests.Collections.CircularBufferTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes` (fix with `dotnet format zerox.csharp.core.slnx`)
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Lower bound on a sorted span | `src/Zerox.Core/Collections/SortedSearch.cs` :: `SortedSearch.LowerBound` (exists, stage A) | 01 |
| Window over a sorted sequence (from inclusive, to exclusive, last N) | `src/Zerox.Core/Collections/Interval.cs` :: `Interval<T>`, `Interval` — created by block 01 | 01, 03, 04 |
| Ring buffer with interval views | `src/Zerox.Core/Collections/CircularBuffer.cs` :: `CircularBuffer<T>` — created by block 01 | 01, 03, 04, 05 |
| Order key/value pairs by key | `src/Zerox.Core/Collections/KeyComparer.cs` :: `KeyComparer.For<TKey, TValue>()` — created by block 01 | 01, 03, 04 |
| Key of a new sample, distance between keys | `src/Zerox.Core/Statistics/SeriesKeys.cs` :: `ISeriesKeys<TKey>`, `TimeKeys`, `IndexKeys` — created by block 02 | 02, 03, 04, 07 |
| What the aggregator and the provider see of a series | `src/Zerox.Core/Statistics/ISeries.cs` :: `ISeries`, `IValueSeries<TValue>`, `SeriesUpdate`, `SeriesWindow.NewPoints` — created by block 03 | 03, 04, 05, 06, 08 |
| Registry of series and groups | `src/Zerox.Core/Statistics/SeriesAggregator.cs` :: `SeriesAggregator` — created by block 06 | 06, 07, 08 |
| Hierarchical id | `src/Zerox.Core/Identity/HierId.cs` :: `HierId` (exists, stage A) | 03, 04, 05, 06, 07 |
| Properties carrying an attribute; compiled getter | `src/Zerox.Core/Reflection/AttributeExtensions.cs` :: `GetPropertiesWith`; `src/Zerox.Core/Reflection/FastProperty.cs` :: `FastProperty.For` (exist, stage A) | 07 |
| Detached start whose escaping exception stops the process | `src/Zerox.Core/Concurrency/OwnerQueue.cs` :: `OwnerQueue.StartDetached` (exists, stage A) | 08 |
| Fake clock in tests | `Microsoft.Extensions.TimeProvider.Testing` :: `FakeTimeProvider` (package, referenced by the test project) | 02, 03, 06, 07, 08 |

## Execution Order

01-interval-and-circular-buffer → no dependencies
02-series-keys                  → no dependencies
03-data-series                  → depends on 01, 02
04-array-series                 → depends on 01, 02, 03 (ISeries)
05-cyclic-series                → depends on 01, 03 (ISeries)
06-aggregator                   → depends on 03, 04 (test uses ArraySeries)
07-aggregable-sources           → depends on 02, 03, 06
08-remote-data-provider         → depends on 03, 06
</mission>

<files>
- 01-collections.md
- 02-series.md
- 03-aggregation.md
</files>
