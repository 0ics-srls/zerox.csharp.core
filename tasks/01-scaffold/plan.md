# TDDAB Plan: zerox.csharp.core scaffold
**Date:** 2026-10-26

<mission>
## Project

`zerox.csharp.core` is a new C# library repository at `/Users/mail0ics.it/dev/zerox.csharp.core`
(branch `main`, no remote yet: never push). It will hold the reusable core of the old Zerox code,
rebuilt clean; this plan builds ONLY the skeleton. No capability of the old code is ported here.

Three production projects and one test project each:

| Project | Path | Depends on |
|---|---|---|
| `Zerox.Core` | `src/Zerox.Core/Zerox.Core.csproj` | nothing (later only `Microsoft.Extensions.*.Abstractions`, `System.Text.Json`) |
| `Zerox.Core.Avalonia` | `src/Zerox.Core.Avalonia/Zerox.Core.Avalonia.csproj` | `Zerox.Core`, package `Avalonia` |
| `Zerox.Dnf` | `src/Zerox.Dnf/Zerox.Dnf.csproj` | nothing |
| `Zerox.Core.Tests` | `tests/Zerox.Core.Tests/Zerox.Core.Tests.csproj` | `Zerox.Core` |
| `Zerox.Core.Avalonia.Tests` | `tests/Zerox.Core.Avalonia.Tests/Zerox.Core.Avalonia.Tests.csproj` | `Zerox.Core.Avalonia`, packages `Avalonia`, `Avalonia.Headless` |
| `Zerox.Dnf.Tests` | `tests/Zerox.Dnf.Tests/Zerox.Dnf.Tests.csproj` | `Zerox.Dnf` |

## Stack (locked)

- .NET SDK 10.0.400 (`global.json`, rollForward `latestFeature`, test runner Microsoft.Testing.Platform), `net10.0`, C# 14 (`LangVersion latest`), Nullable, ImplicitUsings.
- Solution `zerox.csharp.core.slnx` (XML solution format), Central Package Management with transitive pinning in `Directory.Packages.props`; `.csproj` files never carry versions.
- Package versions: `Avalonia` 12.1.4, `Avalonia.Headless` 12.1.4, `xunit.v3.mtp-v2` 4.0.2, `AwesomeAssertions` 9.6.0. Nothing else.
- Tests: xunit v3 on the Microsoft Testing Platform, AwesomeAssertions (never FluentAssertions). Avalonia tests use `HeadlessUnitTestSession` from `Avalonia.Headless` with plain `[Fact]` (the `Avalonia.Headless.XUnit` package is built for xunit v3 3.x and does not work with 4.x: do not add it).
- Test names: `Should_<Outcome>_When<Condition>`.
- Build output in `artifacts/` (`UseArtifactsOutput`).
- Version: `version.build` holds the build number; every assembly is `1.0.<n>`.

## Rules

- Everything in the repository is English: identifiers, comments, strings, files, commit messages.
- Zero warnings: `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. `.editorconfig` raises CA2007, CS8618, CA1849, IDE0005 to errors; IDE0005 needs `GenerateDocumentationFile=true`, so the build sets it and silences CS1591 (missing XML comments).
- In tests CA2007 is off (xunit forbids `ConfigureAwait(false)` in tests, rule xUnit1030), CA1707 is off (underscores in test names) and CA1515 is off.
- KISS, DRY, SOLID as heuristics: no interface without a consumer that substitutes it, no registration method without something to wire, one implementation per capability.
- P9 — no exception is hidden: an exception nobody expected is never caught to go on.
- Constructor injection only, no DI container inside components. `internal sealed` by default; `InternalsVisibleTo` gives each production project's internals to its `*.Tests` project.
- Commit per block (the planexecutor does it). Stage files by path, never the whole tree. Commit messages end with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- All tests: `dotnet test --solution zerox.csharp.core.slnx`
- One project: `dotnet test --project tests/Zerox.Dnf.Tests`
- Format check: `dotnet format zerox.csharp.core.slnx --verify-no-changes`
- ast-grep layer: `bash .claude/support/lint/lint-run.sh` (exit 0 clean, 1 findings, 2 sgconfig.yml missing, 3 ast-grep not installed = skip with a warning)
- Everything (after block 03): `tools/verify.sh`

## Files that already exist (do not recreate, extend)

- `Directory.Build.props` — the lint template: Nullable, TreatWarningsAsErrors, AnalysisLevel latest-recommended, EnforceCodeStyleInBuild. Block 01 adds the rest.
- `.editorconfig` — the lint template (`root = true`, `[*.cs]` with CA2007, CS8618, CA1849, IDE0005 as errors, a `should_when` naming style). Block 01 adds the style and the test overrides.
- `.gitignore` — `.cvm`, `.lsai/`, `cortex_db/`. Block 01 appends.
- `sgconfig.yml`, `j-settings.md`, `.mcp.json`, `.claude/`, `.ai-agent/`, `memory-bank/`, `CLAUDE.md` (template; block 03 rewrites the project part).

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Every assembly's version from one file | `Directory.Build.props` (`_BuildNumber` read from `version.build`) — created by block 01 | 01, 02, 03 |
| Test project configured by its name (runner, assertions, usings, shared test source, InternalsVisibleTo) | `Directory.Build.targets` — created by block 01 | 01, 02 |
| Find the repository root and the build number from a running test | `tests/Shared/RepositoryRoot.cs` :: `RepositoryRoot.Folder`, `RepositoryRoot.File(...)`, `RepositoryRoot.VersionBuild()` — created by block 01 in the shape of assetforge's `RepositoryRoot` (another repository: not referenceable), linked into every test project | 01 |
| Run a test body on the headless Avalonia UI thread | `tests/Zerox.Core.Avalonia.Tests/HeadlessUi.cs` :: `HeadlessUi.RunAsync` over `Avalonia.Headless.HeadlessUnitTestSession` (package, exists) — created by block 02 | 02 |
| ast-grep lint layer | `.claude/support/lint/lint-run.sh` — EXISTS (ai-agent); call it, never copy it | 03 |

## Execution Order

01-solution-skeleton   → no dependencies
02-headless-ui-session → depends on 01
03-verify-and-docs     → depends on 01, 02
</mission>

<block id="01-solution-skeleton">
## TDDAB-1: Create the Solution Skeleton with Its Build Foundations

<intro>
Creates the whole build skeleton of `/Users/mail0ics.it/dev/zerox.csharp.core`: `global.json`,
`version.build`, the completed `Directory.Build.props`, `Directory.Build.targets`,
`Directory.Packages.props`, `.editorconfig` additions, `.gitignore` additions,
`zerox.csharp.core.slnx`, the three production projects (empty), the three test projects and the
shared test helper `tests/Shared/RepositoryRoot.cs`.

Capabilities: the version from one file and the test-project convention are reused from
assetforge (same MSBuild lines, written here, not referenced). Finding the repository root from a
test exists in assetforge (`tests/Assetforge.Test.Common/RepositoryRoot.cs`), which this repository
cannot reference: this block writes `RepositoryRoot` in the same shape, once, linked into every test
project by `Directory.Build.targets` (never copied).

The production assemblies are empty. A test loads them by name with
`Assembly.Load(new AssemblyName("Zerox.Core"))` (the project reference copies the dll next to the
test).
</intro>

<red>
- test: Should_TakeTheVersionFromVersionBuild_WhenZeroxCoreIsBuilt — `Zerox.Core` assembly version equals `new Version(1, 0, RepositoryRoot.VersionBuild(), 0)`
- test: Should_ReferenceOnlyTheFramework_WhenZeroxCoreIsBuilt — every referenced assembly name of `Zerox.Core` starts with `System` or is `netstandard`; none contains `Avalonia`, `Newtonsoft`, `Autofac`
- test: Should_TakeTheVersionFromVersionBuild_WhenZeroxCoreAvaloniaIsBuilt — same for `Zerox.Core.Avalonia`
- test: Should_TakeTheVersionFromVersionBuild_WhenZeroxDnfIsBuilt — same for `Zerox.Dnf`
- test: Should_ReferenceOnlyTheFramework_WhenZeroxDnfIsBuilt — every referenced assembly name of `Zerox.Dnf` starts with `System` or is `netstandard`
- test: Should_FindTheRepositoryRoot_WhenRunFromTheTestOutput — `RepositoryRoot.Folder` contains `zerox.csharp.core.slnx` and `version.build`; `RepositoryRoot.File("version.build")` is that file's full path
- test: Should_ReadTheBuildNumber_WhenVersionBuildHoldsANumber — `RepositoryRoot.VersionBuild()` equals `int.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Folder, "version.build")).Trim())` and is ≥ 1
</red>

### Implementation

`/Users/mail0ics.it/dev/zerox.csharp.core/global.json`
```json
{
  "sdk": { "version": "10.0.400", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

`/Users/mail0ics.it/dev/zerox.csharp.core/version.build`
```
1
```

`/Users/mail0ics.it/dev/zerox.csharp.core/Directory.Build.props` (keep the four existing lint properties, add the rest)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <!-- IDE0005 (unnecessary usings) is reported in the build only with the documentation file on -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <UseArtifactsOutput>true</UseArtifactsOutput>
    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <NuGetAuditLevel>low</NuGetAuditLevel>
    <_BuildNumber>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)version.build').Trim())</_BuildNumber>
    <Version>1.0.$(_BuildNumber)</Version>
    <Company>0ics s.r.l.s.</Company>
    <Product>zerox.csharp.core</Product>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  </PropertyGroup>
</Project>
```

`/Users/mail0ics.it/dev/zerox.csharp.core/Directory.Build.targets`
```xml
<Project>
  <PropertyGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
    <PackageReference Include="xunit.v3.mtp-v2" />
    <PackageReference Include="AwesomeAssertions" />
    <Using Include="Xunit" />
    <Using Include="AwesomeAssertions" />
    <Compile Include="$(MSBuildThisFileDirectory)tests/Shared/*.cs" Link="Shared/%(Filename)%(Extension)" />
  </ItemGroup>
  <ItemGroup Condition="!$(MSBuildProjectName.EndsWith('.Tests'))">
    <InternalsVisibleTo Include="$(MSBuildProjectName).Tests" />
  </ItemGroup>
</Project>
```

`/Users/mail0ics.it/dev/zerox.csharp.core/Directory.Packages.props`
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Avalonia" Version="12.1.4" />
    <PackageVersion Include="Avalonia.Headless" Version="12.1.4" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="4.0.2" />
    <PackageVersion Include="AwesomeAssertions" Version="9.6.0" />
  </ItemGroup>
</Project>
```

`/Users/mail0ics.it/dev/zerox.csharp.core/.editorconfig` — keep the template lines, append:
```ini
[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4
csharp_style_namespace_declarations = file_scoped:error
dotnet_style_qualification_for_field = false
dotnet_style_qualification_for_property = false
dotnet_style_qualification_for_method = false
dotnet_style_qualification_for_event = false
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

[tests/**.cs]
dotnet_diagnostic.CA2007.severity = none
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1515.severity = none

[*.{json,xml,csproj,props,targets,slnx}]
indent_style = space
indent_size = 2
```
(`root = true` stays the first line of the file; the `[*]` section goes right after it.)

`/Users/mail0ics.it/dev/zerox.csharp.core/.gitignore` — append:
```
artifacts/
TestResults/
.vs/
.idea/
*.user
.DS_Store
```

`/Users/mail0ics.it/dev/zerox.csharp.core/zerox.csharp.core.slnx`
```xml
<Solution>
  <Folder Name="/Solution Items/">
    <File Path=".editorconfig" />
    <File Path="Directory.Build.props" />
    <File Path="Directory.Build.targets" />
    <File Path="Directory.Packages.props" />
    <File Path="global.json" />
    <File Path="version.build" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/Zerox.Core/Zerox.Core.csproj" />
    <Project Path="src/Zerox.Core.Avalonia/Zerox.Core.Avalonia.csproj" />
    <Project Path="src/Zerox.Dnf/Zerox.Dnf.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Zerox.Core.Tests/Zerox.Core.Tests.csproj" />
    <Project Path="tests/Zerox.Core.Avalonia.Tests/Zerox.Core.Avalonia.Tests.csproj" />
    <Project Path="tests/Zerox.Dnf.Tests/Zerox.Dnf.Tests.csproj" />
  </Folder>
</Solution>
```

Production projects:
```xml
<!-- src/Zerox.Core/Zerox.Core.csproj and src/Zerox.Dnf/Zerox.Dnf.csproj -->
<Project Sdk="Microsoft.NET.Sdk" />

<!-- src/Zerox.Core.Avalonia/Zerox.Core.Avalonia.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../Zerox.Core/Zerox.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" />
  </ItemGroup>
</Project>
```

Test projects (runner, assertions and the shared source come from `Directory.Build.targets`):
```xml
<!-- tests/Zerox.Core.Tests/Zerox.Core.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../src/Zerox.Core/Zerox.Core.csproj" />
  </ItemGroup>
</Project>

<!-- tests/Zerox.Core.Avalonia.Tests/Zerox.Core.Avalonia.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../src/Zerox.Core.Avalonia/Zerox.Core.Avalonia.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Headless" />
  </ItemGroup>
</Project>

<!-- tests/Zerox.Dnf.Tests/Zerox.Dnf.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../src/Zerox.Dnf/Zerox.Dnf.csproj" />
  </ItemGroup>
</Project>
```

`/Users/mail0ics.it/dev/zerox.csharp.core/tests/Shared/RepositoryRoot.cs` (same shape as assetforge `tests/Assetforge.Test.Common/RepositoryRoot.cs`, which this repository cannot reference)
```csharp
namespace Zerox.Tests.Shared;

internal static class RepositoryRoot
{
    // the folder that holds zerox.csharp.core.slnx, found walking up from AppContext.BaseDirectory
    public static string Folder { get; } = Find();

    public static string File(params string[] parts) => Path.Combine([Folder, .. parts]);

    public static int VersionBuild()
        => int.Parse(System.IO.File.ReadAllText(File("version.build")).Trim(), CultureInfo.InvariantCulture);

    private static string Find()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "zerox.csharp.core.slnx")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("zerox.csharp.core.slnx not found walking up from the test output");
    }
}
```
A missing solution file is a broken checkout, not an expected case: the exception is thrown and never caught (P9).

The headless session's `Dispatch` returns the task of the dispatched body; whether it hands back the body's exception unchanged is pinned by the RED test of block 02, not assumed.

`/Users/mail0ics.it/dev/zerox.csharp.core/tests/Zerox.Core.Tests/AssemblyTests.cs` (the Dnf and Avalonia test projects get the same shape for their own assembly; `RepositoryRootTests` lives in `Zerox.Core.Tests`)
```csharp
using System.Reflection;
using Zerox.Tests.Shared;

namespace Zerox.Core.Tests;

public sealed class AssemblyTests
{
    private static readonly Assembly ZeroxCore = Assembly.Load(new AssemblyName("Zerox.Core"));

    [Fact]
    public void Should_TakeTheVersionFromVersionBuild_WhenZeroxCoreIsBuilt()
        => ZeroxCore.GetName().Version.Should().Be(new Version(1, 0, RepositoryRoot.VersionBuild(), 0));

    [Fact]
    public void Should_ReferenceOnlyTheFramework_WhenZeroxCoreIsBuilt()
        => ZeroxCore.GetReferencedAssemblies().Select(name => name.Name!)
            .Should().OnlyContain(name => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard");
}

public sealed class RepositoryRootTests
{
    [Fact]
    public void Should_FindTheRepositoryRoot_WhenRunFromTheTestOutput()
    {
        File.Exists(Path.Combine(RepositoryRoot.Folder, "zerox.csharp.core.slnx")).Should().BeTrue();
        RepositoryRoot.File("version.build").Should().Be(Path.Combine(RepositoryRoot.Folder, "version.build"));
        File.Exists(RepositoryRoot.File("version.build")).Should().BeTrue();
    }

    [Fact]
    public void Should_ReadTheBuildNumber_WhenVersionBuildHoldsANumber()
    {
        var expected = int.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Folder, "version.build")).Trim(), CultureInfo.InvariantCulture);

        RepositoryRoot.VersionBuild().Should().Be(expected).And.BeGreaterThanOrEqualTo(1);
    }
}
```
`tests/Zerox.Core.Avalonia.Tests/AssemblyTests.cs` has only the version test (an empty assembly keeps no metadata reference to Avalonia, so a reference test would prove nothing). `tests/Zerox.Dnf.Tests/AssemblyTests.cs` has the version and the framework-only tests.

<success>
- [ ] `dotnet build zerox.csharp.core.slnx -warnaserror` succeeds with 0 warnings and 0 errors
- [ ] `dotnet test --solution zerox.csharp.core.slnx` runs the 7 tests of this block and all pass
- [ ] `dotnet format zerox.csharp.core.slnx --verify-no-changes` exits 0
- [ ] No `.csproj` carries a `Version=` attribute (`grep -rn 'Version=' --include='*.csproj' src tests` is empty)
- [ ] `artifacts/` is ignored by git (`git check-ignore artifacts` prints it)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="02-headless-ui-session">
## TDDAB-2: Run Avalonia Tests on the Headless UI Thread

<intro>
Gives `tests/Zerox.Core.Avalonia.Tests` the headless Avalonia session that the UI tests of the
porting task will use: a test application on the headless platform and the helper
`HeadlessUi.RunAsync(Func<Task>)` that runs a test body on the UI thread.

Capability: running UI code without a display is `Avalonia.Headless.HeadlessUnitTestSession`
(package 12.1.4, already in `Directory.Packages.props` from block 01). This block does NOT write a
dispatcher or a session of its own: it starts that session once per test assembly
(`HeadlessUnitTestSession.GetOrStartForAssembly`) and dispatches through it. The helper has the
same shape as assetforge's `tests/Assetforge.Ui.Tests/HeadlessUi.cs`.

`Zerox.Core.Avalonia` has no `App` class, so the test application is the plain
`Avalonia.Application`. An exception thrown inside the UI body must reach the test unchanged
(P9): the helper never catches.

Files: `tests/Zerox.Core.Avalonia.Tests/TestApp.cs`, `tests/Zerox.Core.Avalonia.Tests/HeadlessUi.cs`,
`tests/Zerox.Core.Avalonia.Tests/HeadlessUiTests.cs`.
</intro>

<red>
- test: Should_RunOnTheUiThread_WhenTheBodyIsDispatchedThroughTheSession — inside `HeadlessUi.RunAsync`, `Dispatcher.UIThread.CheckAccess()` is true
- test: Should_NotRunOnTheUiThread_WhenTheTestBodyRunsDirectly — outside `HeadlessUi.RunAsync`, after the session has started, `Dispatcher.UIThread.CheckAccess()` is false
- test: Should_ReachTheTestWithTheSameException_WhenTheUiBodyThrows — `HeadlessUi.RunAsync(() => throw new InvalidOperationException("ui failed"))` throws `InvalidOperationException` with message `ui failed`
- test: Should_ContinueOnTheUiThread_WhenTheBodyAwaits — after `await Task.Yield()` inside the body, `Dispatcher.UIThread.CheckAccess()` is still true
</red>

### Implementation

`/Users/mail0ics.it/dev/zerox.csharp.core/tests/Zerox.Core.Avalonia.Tests/TestApp.cs`
```csharp
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Zerox.Core.Avalonia.Tests.TestApp))]

namespace Zerox.Core.Avalonia.Tests;

// The plain Avalonia application on the headless platform: the library has no App of its own.
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

`/Users/mail0ics.it/dev/zerox.csharp.core/tests/Zerox.Core.Avalonia.Tests/HeadlessUi.cs`
```csharp
using Avalonia.Headless;

namespace Zerox.Core.Avalonia.Tests;

// Runs a test body on the thread of the headless UI (one session per test assembly, app from TestApp).
// Avalonia.Headless.XUnit 12.1.4 is built for xunit v3 3.x: with xunit v3 4.x the session is used directly.
internal static class HeadlessUi
{
    private static HeadlessUnitTestSession Session => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApp).Assembly);

    public static Task RunAsync(Func<Task> body)
        => Session.Dispatch(async () =>
        {
            await body();
            return true;
        }, TestContext.Current.CancellationToken);
}
```

`/Users/mail0ics.it/dev/zerox.csharp.core/tests/Zerox.Core.Avalonia.Tests/HeadlessUiTests.cs`
```csharp
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Tests;

public sealed class HeadlessUiTests
{
    [Fact]
    public Task Should_RunOnTheUiThread_WhenTheBodyIsDispatchedThroughTheSession()
        => HeadlessUi.RunAsync(() =>
        {
            Dispatcher.UIThread.CheckAccess().Should().BeTrue();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Should_NotRunOnTheUiThread_WhenTheTestBodyRunsDirectly()
    {
        await HeadlessUi.RunAsync(() => Task.CompletedTask);   // the session is started

        Dispatcher.UIThread.CheckAccess().Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReachTheTestWithTheSameException_WhenTheUiBodyThrows()
    {
        var run = () => HeadlessUi.RunAsync(() => throw new InvalidOperationException("ui failed"));

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("ui failed");
    }

    [Fact]
    public Task Should_ContinueOnTheUiThread_WhenTheBodyAwaits()
        => HeadlessUi.RunAsync(async () =>
        {
            await Task.Yield();
            Dispatcher.UIThread.CheckAccess().Should().BeTrue();
        });
}
```

<success>
- [ ] `dotnet test --project tests/Zerox.Core.Avalonia.Tests` runs the 4 tests of this block plus the block-01 version test, all pass
- [ ] `tests/Zerox.Core.Avalonia.Tests` does not reference the package `Avalonia.Headless.XUnit`
- [ ] `dotnet build zerox.csharp.core.slnx -warnaserror` has 0 warnings
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="03-verify-and-docs">
## TDDAB-3: Add the Verify Script, CLAUDE.md and README

<intro>
Adds `tools/verify.sh`, the one command that runs every check of the repository with k/n
progress and stops at the first failure, and writes the project documentation:
the project part of `CLAUDE.md` and a new `README.md`.

Capability: the script shape is reused from assetforge `tools/verify.sh` (a `step <k> <name>
<function>` helper printing `[k/N] name`, `[k/N] ok` or `[k/N] FAILED` and exiting 1). The ast-grep
layer is `.claude/support/lint/lint-run.sh` (ai-agent, EXISTS): the script calls it and treats its
exit 3 (ast-grep not installed) as a skip with a warning, never copies it.

The four checks, in order: build with warnings as errors, format check, all tests, ast-grep.

`CLAUDE.md` exists as the ai-agent template (memory bank, code navigation, the auto-loaded memory
bank list at the end). Keep all of it; insert the project part (sections below) after the
`# CODING & INTERACTION NOTES` paragraph, before `## Memory Bank - Critical System`.
</intro>

<red>
- test: tools/verify.sh on the clean repository prints `[1/4] build`, `[1/4] ok` … `[4/4] ok` in order and exits 0
- test: tools/verify.sh with a probe file `src/Zerox.Core/VerifyProbe.cs` holding `internal sealed class VerifyProbe { private int _unused; }` prints `[1/4] FAILED`, never prints `[2/4]`, and exits 1 (the probe file is deleted right after the check)
- test: tools/verify.sh run from another folder (`cd /tmp && /Users/mail0ics.it/dev/zerox.csharp.core/tools/verify.sh`) behaves as from the root and exits 0
</red>

### Implementation

`/Users/mail0ics.it/dev/zerox.csharp.core/tools/verify.sh` (executable, `chmod +x`)
```bash
#!/usr/bin/env bash
# Every check of the repository, in order, with progress [k/4]; stops at the first that fails.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
TOTAL=4

step() {   # step <k> <name> <function>
  echo "[$1/$TOTAL] $2"
  if "$3"; then
    echo "[$1/$TOTAL] ok"
  else
    echo "[$1/$TOTAL] FAILED"
    exit 1
  fi
}

build() { dotnet build zerox.csharp.core.slnx -warnaserror; }
format() { dotnet format zerox.csharp.core.slnx --verify-no-changes; }
tests() { dotnet test --solution zerox.csharp.core.slnx; }
# exit 3 = ast-grep not installed: a warning, not a failure
astgrep() {
  local code=0
  bash .claude/support/lint/lint-run.sh || code=$?
  if [[ $code -eq 3 ]]; then echo "ast-grep not installed: layer skipped"; return 0; fi
  return $code
}

step 1 build build
step 2 format format
step 3 tests tests
step 4 ast-grep astgrep
```

`/Users/mail0ics.it/dev/zerox.csharp.core/CLAUDE.md` — project part to insert:
```markdown
## Project

zerox.csharp.core: the reusable C# core of 0ics, rebuilt clean from the old Zerox code. Three
libraries: `Zerox.Core` (no UI), `Zerox.Core.Avalonia` (UI helpers on Avalonia), `Zerox.Dnf`
(boolean rule compiler). Consumed by assetforge as a submodule.

## Stack locks

- .NET 10 (SDK 10.0.400, C# 14), solution `zerox.csharp.core.slnx`, central package management in `Directory.Packages.props` (no version in a `.csproj`)
- Projects: `src/Zerox.Core` (only `Microsoft.Extensions.*.Abstractions` and `System.Text.Json` may be added), `src/Zerox.Core.Avalonia` (Avalonia 12.1.4, references `Zerox.Core`), `src/Zerox.Dnf` (no dependency); tests in `tests/<Project>.Tests`
- Tests: xunit.v3.mtp-v2 4.0.2 + AwesomeAssertions 9.6.0 on the Microsoft Testing Platform; names `Should_<Outcome>_When<Condition>`; Avalonia tests through `HeadlessUi.RunAsync` (Avalonia.Headless 12.1.4 `HeadlessUnitTestSession` + plain `[Fact]`; never Avalonia.Headless.XUnit)
- Version: `version.build` → every assembly `1.0.<n>`

## Rules

- Everything in the repository is English (identifiers, comments, strings, files, commits)
- KISS, DRY, SOLID as heuristics, applied where a change is real or very likely: no interface without a consumer that substitutes it (the consumer owns it), no registration method without something to wire, one implementation per capability, one actor per class, composition instead of deep inheritance
- No exception is hidden: an expected failure is a Try-pattern or a result; misuse is an `ArgumentException`; an exception nobody expected is never caught to go on
- Constructor injection only, no DI container inside the components; `internal sealed` by default, `public` is a decision
- Library code awaits with `ConfigureAwait(false)` (CA2007); UI code in `Zerox.Core.Avalonia` returns to the UI thread through the `Dispatcher`, explicitly
- Enums instead of strings (in JSON the member name); System.Text.Json only (never Newtonsoft); everything async

## Build & Test

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror` (0 warnings)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- All tests: `dotnet test --solution zerox.csharp.core.slnx`
- Everything: `tools/verify.sh` ([1/4] build, [2/4] format, [3/4] tests, [4/4] ast-grep)
```

`/Users/mail0ics.it/dev/zerox.csharp.core/README.md`
```markdown
# zerox.csharp.core

The reusable C# core of 0ics.

| Project | What it holds | Depends on |
|---|---|---|
| `Zerox.Core` | concurrency, cancellation, collections, realtime statistics, messaging, reflection, extensions | the framework |
| `Zerox.Core.Avalonia` | throttled property notifications, async commands | `Zerox.Core`, Avalonia 12.1.4 |
| `Zerox.Dnf` | boolean rule compiler (expression → disjunctive normal form → masks) | nothing |

## Build and test

    dotnet build zerox.csharp.core.slnx -warnaserror
    dotnet test --solution zerox.csharp.core.slnx
    tools/verify.sh        # every check, [k/4]

Requires the .NET SDK 10.0.400 (see `global.json`).
```

<success>
- [ ] `tools/verify.sh` exits 0 on the clean repository and prints `[1/4] ok` … `[4/4] ok`
- [ ] With the probe file it exits 1 at `[1/4] FAILED`; the probe file is gone afterwards (`git status --short src` is empty)
- [ ] `CLAUDE.md` still holds the ai-agent sections (Memory Bank, Code Navigation, the auto-loaded list) and now the Project, Stack locks, Rules, Build & Test sections
- [ ] `README.md` exists with the three projects and the build commands
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

## Execution Order

01-solution-skeleton   → no dependencies
02-headless-ui-session → depends on 01
03-verify-and-docs     → depends on 01, 02
