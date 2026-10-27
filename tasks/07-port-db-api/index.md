# TDDAB Plan: port the DB → API foundation (context, repository, unit of work, controller)
**Date:** 2026-10-27

<mission>
## Project

`zerox.csharp.core` is the reusable C# core of 0ics. Every path in this plan is relative to the
repository root. Work on branch `feature/07-port-db-api`; commit per block inside this repository;
**never push**.

This plan adds the foundation every project with a database builds on: context → repository → unit of
work → controller. **zerox gives every layer of a project its piece; the project has the same chain:**

```
zerox:     Zerox.Core     →  Zerox.Db          →  Zerox.Domain        →  Zerox.Api
           zerox's contracts: IDbContext + the    search, validator,     EntityController,
           IEntity, IRepo,   EF implementation    distinct values,       error middleware,
           IUnitOfWork, ...                       last writer wins       JSON as is
           (no dependencies)
                ▲                 ▲                    ▲                      ▲
project:   Core (the APP's  →  Db (entities,   →  Domain (services)  →  Api (controllers)
           own contracts)      context)
```

`Zerox.Core` holds zerox's contracts; an application that has contracts of its own (`ICustomerMerge` in the
sample) keeps them in ITS Core, which holds only contracts and interfaces.

- `src/Zerox.Core` (exists, no dependencies) gains `Data/` (namespace `Zerox.Core.Data`): the contracts
  `IEntity`, `IHasOptimisticLock`, `IRepository<T>`, `IRepositoryFactory`, `IUnitOfWork`,
  `IMassiveOpHandler<T>`, `IFieldsToSet<T>` — none names an EF type, so `Zerox.Core` stays free of EF;
- `src/Zerox.Db` (new, references `Zerox.Core`) — `IDbContext` (it names EF types) and the EF Core
  implementation of every contract above: session, interceptor, repository, unit of work, massive
  operation, optimistic lock, `AddZeroxDb`;
- `src/Zerox.Domain` (new, references `Zerox.Db`) — the DevExtreme search, the options validator,
  distinct text values, last writer wins;
- `src/Zerox.Api` (new, references `Zerox.Domain`) — the base controller, the error middleware, the JSON setup.

Each new project has a test project `tests/<Project>.Tests`. `tests/Zerox.Api.Tests/Sample` is a complete
example application, organised by the project's layers in four folders and namespaces —
`Sample/Core` (`Zerox.Api.Tests.Sample.Core`: the app's own contracts only, `ICustomerMerge`),
`Sample/Db` (`…Sample.Db`: entities, context, uses `Zerox.Core.Data` and `Zerox.Db`), `Sample/Domain` (`…Sample.Domain`: services,
uses `Zerox.Domain`),
`Sample/Api` (`…Sample.Api`: controllers and the host, uses `Zerox.Api`) — each folder using ONLY the
folders before it; tested at every level up to end-to-end over HTTP. Namespaces equal the project names.

The capability comes from Filo, a working project, read-only at `../../home/laco/Gestionale` from this
clone (written `<filo>` below). **Never edit it.** "Port" means: behaviour and tests come over; every
identifier, comment, message and test name is rewritten in English, and **nothing of Filo stays** — no
Filo names, no domain hooks, no Italian, no task numbers. Filo test names become `Should_<Outcome>_When<Condition>`.

## Decisions (taken by the user, not to be reopened)

- Everything rests on `IDbContext`: there is NO base `DbContext` class. A project writes
  `class MyContext(DbContextOptions<MyContext> options) : DbContext(options), IDbContext` and adds nothing:
  `DbContext` already has every member of `IDbContext`.
- The state of a context (open boundary, open massive operation, its transaction) lives in `DbSession`
  (internal), attached to the context instance with `ConditionalWeakTable<object, DbSession>`
  (`DbSession.Of(context)`). The rules on save live in `SessionInterceptor` (internal, ONE singleton
  instance `SessionInterceptor.Instance`), a `SaveChangesInterceptor` added by
  `DbContextOptionsBuilder.UseZeroxSession()`. It is a singleton so the options are identical in every
  scope and EF does not build a new internal service provider per scope.
- On a save the interceptor does, in this order: sync save → `NotSupportedException`; massive operation
  open → `InvalidOperationException`; boundary open and not applying → `SuppressWithResult(0)`
  (swallowed); otherwise `Version++` on every `Modified` entity implementing `IHasOptimisticLock`.
- A project's own rules before saving go in the project's own `SaveChangesInterceptor`. The library has
  no hook.
- One port only: `IRepository<T> where T : class, IEntity`. No tags, no `IRepositoryBase`.
- The base controller has NO `[Authorize]` and no MCP attribute: the project adds its own.
- Out of scope: MCP, API keys, login, roles, a shared test library.

## Stack (locked)

- .NET SDK 10.0.400 (`global.json`), `net10.0`, C# 14, Nullable, ImplicitUsings, artifacts output.
  !! The machine must have SDK 10.0.400: with an older SDK nothing restores.
- Central Package Management in `Directory.Packages.props`: no version in a `.csproj`. Block 01 adds
  `Microsoft.EntityFrameworkCore.Relational` 10.0.0 and `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0;
  block 05 adds `DevExtreme.AspNet.Data` 5.1.0; block 08 adds `Microsoft.AspNetCore.TestHost` 10.0.0.
  `Zerox.Api` uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
- Tests: xunit v3 (`xunit.v3.mtp-v2`) + AwesomeAssertions on the Microsoft Testing Platform, names
  `Should_<Outcome>_When<Condition>`. Database tests use **Sqlite in memory**: one open
  `SqliteConnection("DataSource=:memory:")` per test class (`IAsyncLifetime`), `EnsureCreatedAsync`,
  and a NEW context on the same connection whenever a test must prove what is really on the database.
  Tests never sleep. Every new test project copies the shape of `tests/Zerox.Core.Tests/Zerox.Core.Tests.csproj`.
- Test helpers per test project, written once (see Shared Capabilities): `tests/Zerox.Db.Tests/TestDb.cs`,
  `tests/Zerox.Domain.Tests/TestDb.cs` — no shared test library (out of scope).

## Rules

- Everything English. Zero warnings (`TreatWarningsAsErrors`, analyzers `latest-recommended`, CA2007 in
  `src`: library code awaits with `.ConfigureAwait(false)`). No static members on generic types (CA1000).
- Library exceptions: misuse → `ArgumentException`/`ArgumentNullException`; wrong state →
  `InvalidOperationException`; a request that cannot be executed → `InvalidRequestException` (Domain).
  No `catch` that goes on without rethrowing, except where a block says so and why.
- KISS/DRY/SOLID: an interface only where a consumer substitutes it or where the project implements it;
  one implementation per capability (Shared Capabilities); constructor injection, no container inside the
  components; `internal sealed` by default, `public` is a decision (the contracts, the registration
  methods, `EntityController<T>`, the Domain services, the JSON and error types are public).
- Everything async. No `SaveChanges()` sync anywhere.
- Every block that changes the public surface updates `docs/catalog.md` (a section per project, one line
  per type: need → type → file) and the table in `README.md`, in the same commit.
- A new project is added to `zerox.csharp.core.slnx` in the block that creates it.
- Commit per block, staging files by path. Commit messages end with
  `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.

## Commands

- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- One project: `dotnet test --project tests/Zerox.Db.Tests` (one class:
  `dotnet test --project tests/Zerox.Db.Tests -- --filter-class "Zerox.Db.Tests.UnitOfWorkTests"`)
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes`
- ast-grep: `bash .claude/support/lint/lint-run.sh`
- Everything: `tools/verify.sh`
- The block gate runs the tests of the projects the block touches; the end of the plan runs `tools/verify.sh`.

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| An entity with an int key | `src/Zerox.Core/Data/IEntity.cs` :: `Zerox.Core.Data.IEntity` — created by block 01 | 01–10 |
| The context the library works on | `src/Zerox.Db/IDbContext.cs` :: `IDbContext` — created by block 01 | 01–10 |
| Per-context state and the save rules | `src/Zerox.Db/DbSession.cs` :: `DbSession`, `src/Zerox.Db/SessionInterceptor.cs` :: `SessionInterceptor` — created by block 01, extended by 02, 03, 04 | 01–04 |
| The port to a table | `src/Zerox.Core/Data/IRepository.cs` :: `IRepository<T>`; implementation `src/Zerox.Db/Repository.cs` — created by block 01 | 01–10 |
| Registration | `src/Zerox.Db/ServiceCollectionExtensions.cs` :: `AddZeroxDb<TContext>`, `DbContextOptionsBuilderExtensions.UseZeroxSession` — created by block 01 | 01, 09, 10 |
| The boundary | `src/Zerox.Core/Data/IUnitOfWork.cs`; implementation `src/Zerox.Db/UnitOfWork.cs` — created by block 02 | 02, 03, 10 |
| Optimistic lock (`Version`, pin, detach) | `src/Zerox.Core/Data/IHasOptimisticLock.cs` :: `Zerox.Core.Data.IHasOptimisticLock`, `src/Zerox.Db/ModelBuilderExtensions.cs` :: `ApplyOptimisticLock` — created by block 04 | 04, 07, 08, 10 |
| A request that cannot be executed | `src/Zerox.Domain/InvalidRequestException.cs` — created by block 05 | 05, 06, 08, 09 |
| The search (rows, groups, projection) | `src/Zerox.Domain/Search.cs` :: `Search` — created by block 06 | 06, 09 |
| The DevExtreme result shape (`data`, `totalCount`, `groupCount`, `summary`) | `src/Zerox.Domain/DataSourceResult.cs` :: `DataSourceResult<T>` — created by block 06 | 06, 09 |
| Distinct text values | `src/Zerox.Domain/DistinctTextValues.cs` — created by block 06 | 06, 09 |
| Last writer wins | `src/Zerox.Domain/LastWriterWins.cs` — created by block 07 | 07, 10 |
| Error contract + middleware | `src/Zerox.Api/ApiError.cs`, `ErrorHandlingMiddleware.cs` — created by block 08 | 08, 09, 10 |
| JSON as is | `src/Zerox.Api/ZeroxJson.cs` — created by block 08 | 08, 09, 10 |
| Sqlite test database | `tests/Zerox.Db.Tests/TestDb.cs` (block 01), `tests/Zerox.Domain.Tests/TestDb.cs` (block 05) | their own project |
| The example application | `tests/Zerox.Api.Tests/Sample/*` :: `SampleApp` — created by block 09 | 09, 10 |

## Execution Order

01-db-repository        → no dependencies
02-db-unit-of-work      → depends on 01
03-db-massive-op        → depends on 01, 02
04-db-optimistic-lock   → depends on 01
05-domain-validator     → depends on 01
06-domain-search        → depends on 01, 05
07-domain-last-writer   → depends on 01, 04, 05
08-api-errors-json      → depends on 04, 05
09-api-entity-controller → depends on 01, 06, 08 (creates the sample application)
10-sample-end-to-end    → depends on 02, 03, 07, 09
</mission>

<files>
- 01-db.md
- 02-domain.md
- 03-api.md
</files>
