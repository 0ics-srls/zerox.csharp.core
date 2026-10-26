# TDDAB Plan: port the cyclic job scheduler of the old Zerox code
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Its root is the repository root (written `.`
below); every path in this plan is relative to it. It is checked out as the submodule
`externals/zerox.csharp.core` of assetforge; run every command from the repository root and
commit inside this repository (branch `feature/06-port-scheduler`), never in assetforge, never push.

This plan is stage E of the port of the old Zerox code: the cyclic job scheduler (7 years on 500
machines without a crash) and its loop. Its MODEL is kept as it is (user decision): a queue is a
job; a queue is a ring and each execution runs one element and moves on; the main ring holds the
main jobs and the slow queue (and custom queues), so a lap of the main jobs runs one slow job; a
tick runs one main element and one priority element; one-time jobs leave the ring after running;
delayed one-time jobs wait for their time; groups suspend and resume jobs. The old code is a
read-only copy at `../../tmp/zerox`: `../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/Scheduler/Job.cs`,
`../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/WorkingProcess.cs`, tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Commands/Scheduler/JobTest.cs`, `JobQueueTest.cs`,
`JobSchedulerTest.cs` (NUnit). Never edit it. "Port" means: the capability and its tests come over,
the code is rewritten where the old one breaks a rule below, and every bug fixed on the way is
pinned by a test.

Stages A–D are done and provide, in `src/Zerox.Core`:
- `Collections/CircularLinkedList.cs` :: `CircularLinkedList<T>` (`T : class`): `Add` (duplicate → `ArgumentException`), `Remove` → bool, `Contains`, `Count`, `Next()` (returns the current item, then moves the cursor; null when empty), `Reset()` (cursor to the head), enumeration from the head;
- `Concurrency/OwnerQueue.cs` :: `OwnerQueue.StartDetached(Func<Task> work)` (starts work outside the caller's flow; an exception escaping it stops the process).

New area: `src/Zerox.Core/Scheduling` (namespace `Zerox.Core.Scheduling`), tests in
`tests/Zerox.Core.Tests/Scheduling`.

## Stack (locked)


- .NET SDK 10.0.400, `net10.0`, C# 14 (first-class span conversions: an array argument binds to a `ReadOnlySpan<T>` parameter), Nullable, ImplicitUsings.
- Central Package Management in `Directory.Packages.props`; no version in a `.csproj`. Nothing is added (`FakeTimeProvider` is already referenced by `tests/Zerox.Core.Tests`).
- Tests: xunit v3 (Microsoft Testing Platform) + AwesomeAssertions. Ported tests keep their old names and may keep xunit `Assert`; new tests are named `Should_<Outcome>_When<Condition>` and use AwesomeAssertions. Timing-dependent tests (real threads racing through long loops) carry `[Trait("Category", "Integration")]`.
- Tests never sleep.
- Jobs are SYNCHRONOUS (`Action`): the time of a cycle is the sum of its jobs; the loop around them (`WorkingProcess`) is async on a `TimeProvider`. Scheduler tests drive the ticks by hand (`Tick()`), loop tests use `FakeTimeProvider`.

## Rules

- Everything English: identifiers, comments, strings, test names of new tests, commit messages.
- Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in `src`). Library code awaits with `.ConfigureAwait(false)`. No static members on generic types (CA1000): factories live on a non-generic static class. Library code never throws `IndexOutOfRangeException`, `ApplicationException`, `Exception` (CA2201): misuse is `ArgumentException` / `ArgumentOutOfRangeException` / `ArgumentNullException`, a wrong state is `InvalidOperationException`.
- P9, no exception is hidden: no `catch` that goes on without rethrowing; an exception nobody expected reaches its caller, or, where there is no caller, the process. The ONE exception (user decision): a queue given an error handler (the scheduler's) is the supervisor of its jobs — a job's exception is handed, with the job, to that handler (the scheduler writes it to its errors channel) and the ring goes on; the engine's own failures (the loop) still stop the process.
- KISS, DRY, SOLID: an interface only where a consumer holds several implementations (`IJob`: a ring holds jobs and queues alike); one implementation per capability (see Shared Capabilities); constructor injection, no container, no static service locator; composition before inheritance (one shallow base, `JobBase`, for the job state; a runtime = `JobScheduler` + `WorkingProcess`).
- Public types are the library API (`public sealed` unless a block says otherwise); helpers are `internal`.
- Commit per block, staging files by path. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Core.Tests` (filter one class: `dotnet test --project tests/Zerox.Core.Tests -- --filter-class "Zerox.Core.Tests.Scheduling.JobQueueTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes` (fix with `dotnet format zerox.csharp.core.slnx`)
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Ring: current item, then move on | `src/Zerox.Core/Collections/CircularLinkedList.cs` :: `CircularLinkedList<T>` (exists, stage A) | 02 |
| Detached start whose escaping exception stops the process | `src/Zerox.Core/Concurrency/OwnerQueue.cs` :: `OwnerQueue.StartDetached` (exists, stage A) | 04 |
| A job and its state | `src/Zerox.Core/Scheduling/IJob.cs`, `JobBase.cs` :: `IJob`, `JobBase` — created by block 01 | 01, 02, 03 |
| A ring of jobs | `src/Zerox.Core/Scheduling/JobQueue.cs` :: `JobQueue` — created by block 02 | 02, 03 |
| The scheduler | `src/Zerox.Core/Scheduling/JobScheduler.cs` :: `JobScheduler` — created by block 03 | 03, 04 (runtime test) |
| Fake clock in tests | `Microsoft.Extensions.TimeProvider.Testing` :: `FakeTimeProvider` (package, referenced) | 01, 03, 04 |

## Execution Order

01-jobs              → no dependencies
02-job-queue         → depends on 01
03-job-scheduler     → depends on 01, 02
04-working-process   → depends on 03 (the runtime test composes the scheduler and the loop)
</mission>

<files>
- 01-jobs.md
- 02-scheduler.md
</files>
