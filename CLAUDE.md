# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CODING & INTERACTION NOTES

If you want to ask questions for more spec or other info always use AskUserQuestion tool.

## Project

zerox.csharp.core: the reusable C# core of 0ics, rebuilt clean from the old Zerox code. Libraries:
`Zerox.Core` (no UI), `Zerox.Core.Avalonia` (UI helpers on Avalonia), `Zerox.Dnf` (boolean rule
compiler), and the `Zerox.Ef.*` family (the database → API foundation for applications). Consumed by
assetforge as a submodule.

## Stack locks

- .NET 10 (SDK 10.0.400, C# 14), solution `zerox.csharp.core.slnx`, central package management in `Directory.Packages.props` (no version in a `.csproj`)
- Projects: `src/Zerox.Core` (no UI), `src/Zerox.Core.Avalonia` (Avalonia 12.1.4, references `Zerox.Core`), `src/Zerox.Dnf`, the database → API family `src/Zerox.Ef.{Core,Db,Domain,Api}` (each references the one before; `Zerox.Ef.Core` has no dependency); tests in `tests/<Project>.Tests`
- Tests: xunit.v3.mtp-v2 4.0.2 + AwesomeAssertions 9.6.0 on the Microsoft Testing Platform; names `Should_<Outcome>_When<Condition>`; Avalonia tests through `HeadlessUi.RunAsync` (Avalonia.Headless 12.1.4 `HeadlessUnitTestSession` + plain `[Fact]`; never Avalonia.Headless.XUnit); in a test the UI thread is recognised by `SynchronizationContext.Current is AvaloniaSynchronizationContext` (headless `Dispatcher.UIThread.CheckAccess()` is true on any thread)
- Version: `version.build` → every assembly `1.0.<n>`

## Rules

- Everything in the repository is English (identifiers, comments, strings, files, commits)
- KISS, DRY, SOLID as heuristics, applied where a change is real or very likely: no interface without a consumer that substitutes it (the consumer owns it), no registration method without something to wire, one implementation per capability, one actor per class, composition instead of deep inheritance
- No exception is hidden: an expected failure is a Try-pattern or a result; misuse is an `ArgumentException`; an exception nobody expected is never caught to go on
- Constructor injection only, no DI container inside the components; `internal sealed` by default, `public` is a decision
- Library code awaits with `ConfigureAwait(false)` (CA2007); UI code in `Zerox.Core.Avalonia` returns to the UI thread through the `Dispatcher`, explicitly
- Enums instead of strings (in JSON the member name); System.Text.Json only (never Newtonsoft); everything async
- Paths in the repository are relative to its root; never write a machine's absolute path

## Catalog

`docs/catalog.md` lists what the library offers, by need (type + file). Look there before writing a
capability, then read the type with LSAI. A change to the public surface updates the catalog in the same commit.

## Build & Test

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror` (0 warnings)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- All tests: `dotnet test --solution zerox.csharp.core.slnx`
- Everything: `tools/verify.sh` ([1/4] build, [2/4] format, [3/4] tests, [4/4] ast-grep)

## Memory Bank - Critical System

The Memory Bank is Claude's ONLY connection to the project between sessions. Without it, Claude starts completely fresh with zero knowledge of the project.

### How Memory Bank Works

1. **User triggers**: Type `mb`, `update memory bank`, or `check memory bank`
2. **Claude's process**:
   - Reads `memory-bank/README.md` and follows its instructions

### Important Rules

- Follow instructions in `memory-bank/README.md` - it defines what to read and when
- Memory Bank is the single source of truth - overrides any other documentation

## Project Documentation

Each project has its own README.md with:
- Purpose and architecture
- Build, test, and run commands  
- API usage examples
- Configuration details

Always check the project's README for localized information before working on it.

## Summary of Key Commands

- `mb` or `update memory bank` - Trigger Memory Bank update
- `mcp` - Check MCP server status

## Code Navigation

Code intelligence comes from MCP servers. They answer from the compiler/index; grep answers from
text. Use them for every symbol, usage and "does X exist" lookup.

**Which server this project has is declared in `j-settings.md`, not here** — `@code-nav-local`
for navigating this project's own code, `@code-nav-external` for third-party libraries. Read the
setting and use what it names. This file cannot know: a server is installed per project, the
settings file is the place that says so, and a fixed list here would be an assertion about
somebody else's machine.

- `@code-nav-local` names a server → use it for every symbol, usage and "does X exist" lookup
- `@code-nav-external` names one → use it for a library's real API, instead of recalling it
- either one resolves to `grep` or is absent → grep, **declared as the fallback it is** and the
  verdict flagged low-confidence

Grep is the fallback ONLY when no code-intelligence server is present — and an empty result from a
server that is still indexing means "not ready", NOT "0 matches". The full discipline (per-query
preflight, readiness check, scoping) is `.claude/commands/mind-sets/code-navigation.md`.

**When spawning subagents, ALWAYS include this in the agent prompt verbatim:**
> "Use the code-intelligence MCP server named by `@code-nav-local` / `@code-nav-external` in
> j-settings.md — its symbol search, outline, usages and call-graph tools — instead of
> Grep/Glob/LS for symbol search and project exploration. An empty result from a server that is
> not ready is NOT a zero-result — verify readiness before trusting silence. If no server is
> configured, say so in the answer and flag the grep result as low-confidence."

A subagent inherits none of this by default: if you do not paste it, it will grep.

## Memory Bank — stato reale (AUTO-LOADED)
@memory-bank/README.md
@memory-bank/productContext.md
@memory-bank/systemPatterns.md
@memory-bank/techContext.md
@memory-bank/activeContext.md
@memory-bank/progress.md
MB was READEN!
MB was READEN!
