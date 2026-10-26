# TDDAB Plan: port the foundations of the old Zerox code
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Its root is the repository root (written `.`
below); every path in this plan is relative to it. It is checked out as the submodule
`externals/zerox.csharp.core` of assetforge; run every command from the repository root and
commit inside this repository (branch `feature/02-port`), never in assetforge, never push.

This plan is stage A of the port of the old Zerox code. The old code is a read-only copy at
`../../tmp/zerox` (the assetforge checkout that holds this submodule): `../../tmp/zerox/Zerox.Dnf`,
`../../tmp/zerox/Zerox.Dnf.Tests`, `../../tmp/zerox/Zerox.Wpf.Core/Core`,
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core`. Never edit it; read it when a block says so.
"Port" means: the capability and its tests come over, the code is rewritten where the old one
breaks a rule below, and every bug fixed on the way is pinned by a test.

Projects (all exist, empty, from task 01): `src/Zerox.Core` (namespaces `Zerox.Core.<Area>`,
one folder per area: `Collections`, `Concurrency`, `Cancellation`, `Identity`, `Numbers`, `Text`,
`Reflection`), `src/Zerox.Dnf`, tests in `tests/Zerox.Core.Tests` (same folder per area) and
`tests/Zerox.Dnf.Tests`. Test projects get xunit v3, AwesomeAssertions, `using Xunit` and
`using AwesomeAssertions` from `Directory.Build.targets`; production internals are visible to
the matching `*.Tests` project.

## Stack (locked)

- .NET SDK 10.0.400, `net10.0`, C# 14 (first-class span conversions: an extension on `ReadOnlySpan<T>` applies to arrays and `Span<T>`), Nullable, ImplicitUsings.
- Central Package Management in `Directory.Packages.props`; no version in a `.csproj`. Block 07 adds `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 (test projects only). Nothing else is added.
- Tests: xunit v3 (Microsoft Testing Platform) + AwesomeAssertions. Ported tests keep their old names and may keep xunit `Assert`; new tests are named `Should_<Outcome>_When<Condition>` and use AwesomeAssertions. Timing-dependent tests carry `[Trait("Category", "Integration")]`.
- Time: whatever waits or times out takes a `TimeProvider` in its constructor; tests use `FakeTimeProvider` (namespace `Microsoft.Extensions.Time.Testing`) and never sleep.

## Rules

- Everything English: identifiers, comments, strings, test names of new tests, commit messages. Old Italian comments are translated.
- Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in `src`). Library code awaits with `.ConfigureAwait(false)`. Public constants are PascalCase (CA1707). Library code never throws `IndexOutOfRangeException`, `ApplicationException`, `Exception` (CA2201): misuse is `ArgumentException` / `ArgumentOutOfRangeException` / `ArgumentNullException`, a wrong state is `InvalidOperationException`.
- P9, no exception is hidden: no `catch` that goes on without rethrowing; an exception nobody expected reaches its caller, or, where there is no caller, the process.
- KISS, DRY, SOLID: no interface in this stage (no consumer substitutes these types); one implementation per capability (see Shared Capabilities); constructor injection, no container, no static service locator; immutable where a value is a value.
- Public types are the library API (`public sealed` unless a block says otherwise); helpers are `internal`.
- Commit per block, staging files by path. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Core.Tests` (filter one class: `dotnet test --project tests/Zerox.Core.Tests -- --filter-class "Zerox.Core.Tests.Collections.BitMaskTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes` (fix with `dotnet format zerox.csharp.core.slnx`)
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Lower/upper bound and first/last binary search on a sorted span | `src/Zerox.Core/Collections/SortedSearch.cs` :: `SortedSearch` — created by block 02 | 02; later stage B (circular buffer views) |
| One serial queue per owner | `src/Zerox.Core/Concurrency/OwnerQueue.cs` :: `OwnerQueue` — created by block 08 | 08; later assetforge §3.7 owners |
| Cancellation tree with timeout on a `TimeProvider` | `src/Zerox.Core/Cancellation/CancellationScope.cs` :: `Terminator`, `CancellationScope` — created by block 07 | 07; later stage D (commands) |
| Convert a value to a target type | `src/Zerox.Core/Text/ValueConversion.cs` :: `ValueConversion.ConvertTo` — created by block 12 | 12; later stage C (validation, edit-copy proxy) |
| JSON options of the whole library | `src/Zerox.Core/Text/JsonDefaults.cs` :: `JsonDefaults.Options` — created by block 11 | 11; later stages B and C |
| Fake clock in tests | `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 :: `FakeTimeProvider` (package, exists) — added by block 07 | 07 |

## Execution Order

01-dnf-port                → no dependencies
02-sorted-search           → no dependencies
03-bit-mask                → no dependencies
04-circular-linked-list    → no dependencies
05-atomic-boolean          → no dependencies
06-critical-section        → no dependencies
07-cancellation-scope      → no dependencies
08-owner-queue             → no dependencies
09-hier-id                 → no dependencies
10-number-range            → no dependencies
11-text-and-json           → no dependencies
12-value-conversion        → no dependencies
13-reflection              → no dependencies
</mission>

<files>
- 01-dnf.md
- 02-collections.md
- 03-concurrency.md
- 04-values.md
</files>
