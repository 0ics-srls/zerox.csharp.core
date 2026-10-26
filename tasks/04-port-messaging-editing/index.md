# TDDAB Plan: port the notification base, validation, edit copy and message channel of the old Zerox code
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Its root is the repository root (written `.`
below); every path in this plan is relative to it. It is checked out as the submodule
`externals/zerox.csharp.core` of assetforge; run every command from the repository root and
commit inside this repository (branch `feature/04-port-messaging-editing`), never in assetforge,
never push.

This plan is stage C of the port of the old Zerox code. The old code is a read-only copy at
`../../tmp/zerox` (the assetforge checkout that holds this submodule):
`../../tmp/zerox/Zerox.Wpf.Core/Core/Containers` (`PocoProxy.cs`, `Validation/*`),
`../../tmp/zerox/Zerox.Wpf.Core/Core/Support/PocoExtensions.cs`,
`../../tmp/zerox/Zerox.Wpf.Core/Core/Messaging/MessagesContext.cs`, and the tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/PropertyContainers/PocoProxyTest.cs`,
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Wpf/Popup/PropertyValidationTest.cs`,
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Messaging/MessagingTest.cs` (NUnit). Never edit it.
"Port" means: the capability and its tests come over, the code is rewritten where the old one
breaks a rule below, and every bug fixed on the way is pinned by a test.

Decisions of the user: a MINIMAL notification base in `Zerox.Core` (`NotifyingObject`, no
throttle, no dispatcher: stage D extends it); the edit copy keeps FastDeepCloner and
ObjectsComparer from NuGet, in `Zerox.Core`; the message channel keeps its message in memory only.

Stages A and B are done and provide, in `src/Zerox.Core`:
- `Text/JsonDefaults.cs` :: `JsonDefaults.Options` (the one `JsonSerializerOptions`: names as in C#, enums as member names, read-only);
- `Reflection/FastProperty.cs` :: `FastProperty.For<TInstance, TValue>(PropertyInfo)` → compiled `Get(instance)` / `Set(instance, value)`;
- `Identity/HierId.cs` :: `HierId` (`HierId.Parse("a/b")`, `HierId.From(...)`, `GetSimilarRootSize(other)`).

New areas: `src/Zerox.Core/ComponentModel`, `Validation`, `Editing`, `Messaging` (namespaces
`Zerox.Core.<Area>`), tests in `tests/Zerox.Core.Tests/<Area>`. Test projects get xunit v3,
AwesomeAssertions, `using Xunit` and `using AwesomeAssertions` from `Directory.Build.targets`.

## Stack (locked)

- .NET SDK 10.0.400, `net10.0`, C# 14 (first-class span conversions: an array argument binds to a `ReadOnlySpan<T>` parameter), Nullable, ImplicitUsings.
- Central Package Management in `Directory.Packages.props`; no version in a `.csproj`. Block 03 adds `FastDeepCloner` 1.3.6 and `ObjectsComparer` 1.4.1 to `Directory.Packages.props` and references them from `src/Zerox.Core` (user decision); nothing else is added.
- Tests: xunit v3 (Microsoft Testing Platform) + AwesomeAssertions. Ported tests keep their old names and may keep xunit `Assert`; new tests are named `Should_<Outcome>_When<Condition>` and use AwesomeAssertions. Timing-dependent tests (real threads racing through long loops) carry `[Trait("Category", "Integration")]`.
- Tests never sleep.
- Everything async: an operation that hands data to another object (`ReceiveAsync`, `SendAsync`, `AcceptAsync`, JSON streams) is async; a read-only question (`CanAccept`, `IsChanged`, a rule) is synchronous.

## Rules

- Everything English: identifiers, comments, strings, test names of new tests, commit messages.
- Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in `src`). Library code awaits with `.ConfigureAwait(false)`. No static members on generic types (CA1000): factories live on a non-generic static class. Library code never throws `IndexOutOfRangeException`, `ApplicationException`, `Exception` (CA2201): misuse is `ArgumentException` / `ArgumentOutOfRangeException` / `ArgumentNullException`, a wrong state is `InvalidOperationException`.
- P9, no exception is hidden: no `catch` that goes on without rethrowing; an exception nobody expected reaches its caller, or, where there is no caller, the process.
- KISS, DRY, SOLID: an interface only where a consumer holds several implementations (`IValidatable`, `IMessageSource<TKind>`, `IMessageDestination<TKind>`); one implementation per capability (see Shared Capabilities); constructor injection, no container, no static service locator; composition before inheritance (the validator is a component the validated object holds).
- Public types are the library API (`public sealed` unless a block says otherwise); helpers are `internal`.
- Commit per block, staging files by path. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Core.Tests` (filter one class: `dotnet test --project tests/Zerox.Core.Tests -- --filter-class "Zerox.Core.Tests.Validation.ValidatorTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes` (fix with `dotnet format zerox.csharp.core.slnx`)
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Change notification (set a field, notify on a real change) | `src/Zerox.Core/ComponentModel/NotifyingObject.cs` :: `NotifyingObject` — created by block 01 | 01, 02 (test model), 04, 05; later stage D |
| Validation rules and the errors of an object | `src/Zerox.Core/Validation/Validator.cs` :: `Validator<T>`, `ValidationRules<T>` — created by block 02 | 02; later stage D view models |
| Deep clone by fields, comparison with differences | `src/Zerox.Core/Editing/PocoExtensions.cs` :: `PocoExtensions` — created by block 03 | 03, 04 |
| Edit copy | `src/Zerox.Core/Editing/PocoProxy.cs` :: `PocoProxy<T>` — created by block 04 | 04, 05 (test data) |
| JSON of the whole library | `src/Zerox.Core/Text/JsonDefaults.cs` :: `JsonDefaults.Options` (exists, stage A; block 03 adds public fields) | 03, 04 |
| Compiled property accessor | `src/Zerox.Core/Reflection/FastProperty.cs` :: `FastProperty.For` (exists, stage A) | 04 |
| Hierarchical id | `src/Zerox.Core/Identity/HierId.cs` :: `HierId` (exists, stage A) | 05 |

## Execution Order

01-notifying-object     → no dependencies
02-validation           → depends on 01 (the test model notifies)
03-poco-clone-compare   → no dependencies
04-poco-proxy           → depends on 01, 03
05-message-channel      → depends on 01, 04 (the test message carries a proxy)
</mission>

<files>
- 01-notification-validation.md
- 02-editing.md
- 03-messaging.md
</files>
