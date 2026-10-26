# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CODING & INTERACTION NOTES

If you want to ask questions for more spec or other info always use AskUserQuestion tool.

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
