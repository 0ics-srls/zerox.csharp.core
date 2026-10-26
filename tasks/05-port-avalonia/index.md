# TDDAB Plan: port the UI base of the old Zerox code to Avalonia
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Its root is the repository root (written `.`
below); every path in this plan is relative to it. It is checked out as the submodule
`externals/zerox.csharp.core` of assetforge; run every command from the repository root and
commit inside this repository (branch `feature/05-port-avalonia`), never in assetforge, never push.

This plan is stage D of the port of the old Zerox code: the UI base in `src/Zerox.Core.Avalonia`
(exists, empty; references `Zerox.Core` and Avalonia 12.1.4): a view model base with UI-thread and
throttled notifications, commands, async commands with an observable task and cancellation, a
minimal dialog service. The old code is a read-only copy at `../../tmp/zerox`:
`../../tmp/zerox/Zerox.Wpf.Core/Core/Containers/PropertyContainer.cs`, `MemberContainer.cs`,
`../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/` (`CommandTP.cs`, `AsyncCommandTP.cs`,
`ObservableTask.cs`, `Internal/CancelAsyncCommand.cs`, `ProgressiveAsyncCommandTP.cs`,
`ICommandWithInfo.cs`), `../../tmp/zerox/Zerox.Wpf.Core/Core/Wpf/MessagingContext.cs`, and the
tests `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/PropertyContainers/ThrotlingTest.cs`,
`NestedPropertyChangedTest.cs` (NUnit, WPF). Never edit it. "Port" means: the capability and its
tests come over, the code is rewritten where the old one breaks a rule below (WPF → Avalonia), and
every bug fixed on the way is pinned by a test.

Decisions of the user: the D is the UI base (charts → own stage later; scheduler → stage E;
`ActionsContext` dropped); a MINIMAL dialog service (notify, confirm, error).

Stages A–C are done and provide, in `src/Zerox.Core`:
- `ComponentModel/NotifyingObject.cs` :: `NotifyingObject` (abstract, `INotifyPropertyChanged`): protected virtual `SetProperty(ref field, value, [CallerMemberName] name)` → bool, `NotifyPropertyChanged(name)` (null = every property), `NotifyPropertiesChanged(params ReadOnlySpan<string>)`, protected virtual `OnPropertyChanged(string? name)` through which every notification passes (default: raises `PropertyChanged`);
- `Cancellation/CancellationScope.cs` :: `Terminator(TimeProvider)` with `CreateScope(TimeSpan? timeout = null)` → `CancellationScope` (`Token`, `State` : `CancellationState` {Active, Canceled, TimedOut}, `IsActive`, `Cancel()`, `DelayAsync(TimeSpan)`, `Dispose()`).

Tests: `tests/Zerox.Core.Avalonia.Tests` (exists: `HeadlessUi.cs`, `TestApp.cs` = plain `Application` on the headless platform, no theme). New folders `ComponentModel`, `Commands`, `Dialogs` in source and tests (namespaces `Zerox.Core.Avalonia.<Area>`, tests `Zerox.Core.Avalonia.Tests.<Area>`).

## Stack (locked)


- .NET SDK 10.0.400, `net10.0`, C# 14 (first-class span conversions: an array argument binds to a `ReadOnlySpan<T>` parameter), Nullable, ImplicitUsings.
- Central Package Management in `Directory.Packages.props`; no version in a `.csproj`. Avalonia 12.1.4 (`src/Zerox.Core.Avalonia`), Avalonia.Headless 12.1.4 (tests). Block 01 adds a reference to `Microsoft.Extensions.TimeProvider.Testing` (already in `Directory.Packages.props`) to `tests/Zerox.Core.Avalonia.Tests`; nothing else is added.
- Tests: xunit v3 (Microsoft Testing Platform) + AwesomeAssertions. Ported tests keep their old names and may keep xunit `Assert`; new tests are named `Should_<Outcome>_When<Condition>` and use AwesomeAssertions. Timing-dependent tests (real threads racing through long loops) carry `[Trait("Category", "Integration")]`.
- Tests never sleep.
- UI tests run inside `HeadlessUi.RunAsync(...)` (tests/Zerox.Core.Avalonia.Tests/HeadlessUi.cs: the body runs on the headless UI thread); posted work runs with `Dispatcher.UIThread.RunJobs()` or when the body awaits. The UI thread is recognised by `SynchronizationContext.Current is AvaloniaSynchronizationContext` (headless `Dispatcher.UIThread.CheckAccess()` is true on any thread). Time-driven tests use `FakeTimeProvider` (its timer callbacks run inside `Advance`).
- Threads: UI code returns to the UI thread through the `Dispatcher`, explicitly (`Dispatcher.UIThread.Post` / `InvokeAsync`); library awaits that must come back to the UI thread say `ConfigureAwait(true)`, the others `ConfigureAwait(false)` (CA2007).

## Rules

- Everything English: identifiers, comments, strings, test names of new tests, commit messages.
- Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in `src`). Library code awaits with `.ConfigureAwait(false)`. No static members on generic types (CA1000): factories live on a non-generic static class. Library code never throws `IndexOutOfRangeException`, `ApplicationException`, `Exception` (CA2201): misuse is `ArgumentException` / `ArgumentOutOfRangeException` / `ArgumentNullException`, a wrong state is `InvalidOperationException`.
- P9, no exception is hidden: no `catch` that goes on without rethrowing; an exception nobody expected reaches its caller, or, where there is no caller, the process.
- KISS, DRY, SOLID: an interface only where a consumer holds several implementations (`ICommandWithInfo`, `IDialogService`); one implementation per capability (see Shared Capabilities); constructor injection, no container, no static service locator; composition before inheritance.
- Public types are the library API (`public sealed` unless a block says otherwise); helpers are `internal`.
- Commit per block, staging files by path. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Core.Avalonia.Tests` (filter one class: `dotnet test --project tests/Zerox.Core.Avalonia.Tests -- --filter-class "Zerox.Core.Avalonia.Tests.ComponentModel.PropertyContainerTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes` (fix with `dotnet format zerox.csharp.core.slnx`)
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Change notification | `src/Zerox.Core/ComponentModel/NotifyingObject.cs` :: `NotifyingObject` (exists, stage C) | 01 |
| Cancellation tree with timeout | `src/Zerox.Core/Cancellation/CancellationScope.cs` :: `Terminator`, `CancellationScope` (exist, stage A) | 03 |
| View model base (UI-thread, throttled notifications, nested properties) | `src/Zerox.Core.Avalonia/ComponentModel/PropertyContainer.cs` :: `PropertyContainer` — created by block 01 | 01, 03 (`ObservableTask`) |
| Command shape seen by menus | `src/Zerox.Core.Avalonia/Commands/ICommandWithInfo.cs` :: `ICommandWithInfo` — created by block 02 | 02, 03 |
| Fake clock in tests | `Microsoft.Extensions.TimeProvider.Testing` :: `FakeTimeProvider` (package; referenced by the Avalonia tests from block 01) | 01, 03 |

## Execution Order

01-property-container   → no dependencies
02-command              → no dependencies
03-async-command        → depends on 01 (ObservableTask derives from PropertyContainer), 02 (ICommandWithInfo)
04-dialogs              → no dependencies
</mission>

<files>
- 01-component-model.md
- 02-commands.md
- 03-dialogs.md
</files>
