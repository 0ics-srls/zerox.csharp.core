# 01-scaffold — notes

## Requirement

A new repository `zerox.csharp.core` that will hold the reusable C# core rebuilt from the old
Zerox.Wpf.Core and Zerox.Dnf. This task builds only the skeleton: solution, build and test
foundations, the headless UI test session, the verify script, CLAUDE.md and README. No ported
code: the porting is task 02.

Decisions taken with the user:
- own repository, consumed by assetforge as a submodule in `externals/`;
- three projects: `Zerox.Core` (no UI), `Zerox.Core.Avalonia`, `Zerox.Dnf`;
- the stack of assetforge: .NET 10, C# 14, slnx, CPM, zero warnings, xunit v3 + AwesomeAssertions
  on the Microsoft Testing Platform, System.Text.Json, no Autofac, everything English;
- KISS, DRY and SOLID as heuristics, P9 (no exception is hidden).

## Code Organization

1. **What is shared.** The version number (`version.build`, read once by
   `Directory.Build.props`); the test conventions (`Directory.Build.targets`: every `*.Tests`
   project gets the runner, the assertions and the shared test source by its name); the helper
   that finds the repository root from a test (`tests/Shared/RepositoryFiles.cs`, linked into
   every test project); the headless UI session (only the Avalonia tests use it).
2. **Who owns it.** Build files at the repository root; `tests/Shared/RepositoryFiles.cs` owned
   by the test convention; `tests/Zerox.Core.Avalonia.Tests/HeadlessUi.cs` owned by the Avalonia
   tests.
3. **Contracts.** `RepositoryFiles.Root` / `RepositoryFiles.VersionBuild()` (static, test-only);
   `HeadlessUi.RunAsync(Func<Task>)` (runs a test body on the headless UI thread). Nothing is
   injected: there is nothing to substitute.
4. **What this task does not own.** The ported capabilities (task 02); the GitHub remote (the
   user); the assetforge submodule (an assetforge task).
5. **Mechanism.** Convention over configuration: MSBuild decides by the project name what a test
   project gets; no per-project copy of the test settings.

## Inventario — capacità e dove esistono già

| Capability (what it does) | Where it exists | Decision |
|---|---|---|
| Version of every assembly from one file | assetforge `Directory.Build.props` (`_BuildNumber` from `version.build`) | reuse the same MSBuild lines |
| Test projects configured by their name (runner, assertions, usings, InternalsVisibleTo) | assetforge `Directory.Build.targets` | reuse the same targets |
| Run a test body on the Avalonia UI thread without a display | `Avalonia.Headless` 12.1.4 `HeadlessUnitTestSession`; assetforge `tests/Assetforge.Ui.Tests/HeadlessUi.cs` | reuse the session, same helper shape |
| One script that runs every check with k/n progress and stops at the first failure | assetforge `tools/verify.sh` (`step` function) | reuse the shape, with this repository's four checks |
| ast-grep lint layer | `.claude/support/lint/lint-run.sh` (ai-agent) | call it, do not copy it |
| Find the repository root from a running test | nothing in the framework or in assetforge (searched `version.build` readers: only MSBuild) | write `RepositoryFiles` (one file, linked) |
