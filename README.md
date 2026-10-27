# zerox.csharp.core

The reusable C# core of 0ics.

| Project | What it holds | Depends on |
|---|---|---|
| `Zerox.Core` | concurrency, cancellation, collections, realtime statistics, messaging, reflection, extensions, data contracts (entity, repository) | the framework |
| `Zerox.Core.Avalonia` | throttled property notifications, async commands | `Zerox.Core`, Avalonia 12.1.4 |
| `Zerox.Dnf` | boolean rule compiler (expression → disjunctive normal form → masks) | nothing |
| `Zerox.Db` | the database foundation on EF Core: `IDbContext`, repositories, session rules on save | `Zerox.Core`, EF Core Relational 10 |

What each project offers, by need: [docs/catalog.md](docs/catalog.md).

## Build and test

    dotnet build zerox.csharp.core.slnx -warnaserror
    dotnet test --solution zerox.csharp.core.slnx
    tools/verify.sh        # every check, [k/4]

Requires the .NET SDK 10.0.400 (see `global.json`).
