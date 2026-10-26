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
