# TDDAB Plan: The optimistic lock on the database's row version
**Date:** 2026-10-30

<mission>
## The project

**zerox.csharp.core** is the reusable C# core of 0ics: `Zerox.Core`, `Zerox.Core.Avalonia`, `Zerox.Dnf`
and the **Zerox.Ef** family, the database → API foundation of applications. .NET 10 (SDK 10.0.400, C# 14),
solution `zerox.csharp.core.slnx`, Central Package Management (`Directory.Packages.props`, no version in a
`.csproj`). Branch `feature/10-db-row-version`. Paths below are relative to the repository root.

The Zerox.Ef family, one way only — `Zerox.Ef.Core` → `Zerox.Ef.Db` → `Zerox.Ef.Domain` → `Zerox.Ef.Api`:
```
src/Zerox.Ef/Core/     contracts, no dependency: IEntity, IRepository<T>, IRepositoryFactory, IUnitOfWork,
                       IMassiveOpHandler<T>, IFieldsToSet<T>, IHasOptimisticLock (removed by block 05)
src/Zerox.Ef/Db/       IDbContext + the EF implementation: Repository<T>, SessionInterceptor (the session rules:
                       no sync road, saves swallowed inside a unit of work), DbSession, UnitOfWork,
                       MassiveOpHandler (ExecuteUpdate/ExecuteDelete), ModelBuilderExtensions, AddZeroxDb
src/Zerox.Ef/Domain/   InvalidRequestException (→ 400), Search (DevExtreme load), EntityFields, EntityPaths
                       (IsReserved = [JsonIgnore] and not a navigation; IsNavigation), LastWriterWins
src/Zerox.Ef/Api/      EntityController<T>, ErrorHandlingMiddleware (UseZeroxErrorHandling), ZeroxJson,
                       ZeroxProblemDetails (Code: InvalidRequest 400, NotFound 404, Conflict 409, InternalError 500),
                       ZeroxOpenApi
tests/Zerox.Ef/Db/     Zerox.Ef.Db.Tests      — TestDb.cs (Item, Box, Locked, TestDbContext, TestDb)
tests/Zerox.Ef/Domain/ Zerox.Ef.Domain.Tests  — TestDb.cs (Item, Box, Memo, …, TestDbContext, TestDb)
tests/Zerox.Ef/Api/    Zerox.Ef.Api.Tests     — TestHost.cs (Doc, Plain, HostDbContext, LogCapture, TestHost),
                       Sample/ (the example application: Db/Customer, Db/Order, Db/SampleDbContext,
                       Api/CustomersController, Api/OrdersController, Api/RenameRequest, Api/SampleApp,
                       openapi/Sample.json, client/ = the generated TypeScript client and its tests)
tests/Zerox.Ef/TestDatabase.cs  linked into the three Ef test projects
```

**The test database.** `TestDatabase` gives each test class its database: **Sqlite in memory by default**;
a fresh PostgreSQL database (dropped at the end) when the environment variable `ZEROX_EF_TEST_POSTGRES`
holds a server's connection string. `TestDatabase.OnPostgres` says which. `tools/test-postgres.sh` starts
`postgres:16-alpine` in Docker on `127.0.0.1:55432`, runs the three Ef test projects against it, removes it
(Docker must be running). A test that proves the lock runs only on PostgreSQL and starts with
`Assert.SkipUnless(TestDatabase.OnPostgres, "SQLite has no row version: the lock is proved on PostgreSQL.");`;
a test of the SQLite behaviour starts with `Assert.SkipWhen(TestDatabase.OnPostgres, "SQLite only.");`
(xUnit v3 dynamic skip).

## What this plan changes, and why

The optimistic lock of Zerox.Ef was built wrong. Today `Version` (`IHasOptimisticLock`, `int`) is set by
the application — `SessionInterceptor` writes 0 on an added row and `++` on a modified one — so a write
that does not go through `SaveChanges` (the library's own `MassiveOpHandler`, raw SQL, another process)
leaves it unchanged, and a later PUT with the old version silently overwrites that write (proven on
PostgreSQL). And the PUT reloads the row and copies the body onto it, so `PinReadVersion` has to put the
client's version back by hand.

The design, decided by the user ("it is EF's job to manage it"; "no triggers"; "with SQLite the user is
warned that there is no optimistic lock"):

1. **The row version is EF's and the database's.** An entity declares `[Timestamp]`
   (`System.ComponentModel.DataAnnotations`; EF's `IsRowVersion()` = `ValueGeneratedOnAddOrUpdate()` +
   `IsConcurrencyToken()`) on a property of the type its provider generates: `uint` on PostgreSQL (Npgsql
   maps it to the system column `xmin`, type `xid`: no column of ours), `ulong` or `long` on SQL Server
   (`rowversion`). A **row version**, in this plan, is a property with `IsConcurrencyToken` and
   `ValueGenerated == ValueGenerated.OnAddOrUpdate`. Zerox.Ef never writes it: EF reads it back after every
   INSERT and UPDATE.
2. **A client's row is saved as the client sent it.** `IRepository<T>.Update(entity, fields)` attaches the
   detached entity and marks only `fields` modified (never the key, never what the database generates); EF
   puts the version the client read in the `WHERE`; a row changed since fails the save with
   `DbUpdateConcurrencyException` → 409. `IRepository<T>.TryDeleteById(id, version)` marks a stub
   `{ Id, version }` to delete (false, nothing marked, when the version does not fit the row version's
   type). No reload, no pin.
3. **404 vs 409.** An UPDATE or DELETE of a missing row fails exactly like a stale one (0 rows affected).
   Only after a failed save, one `Any(e => e.Id == id)` read tells them apart: missing → 404, present → the
   exception goes on → 409.
4. **SQLite has no row version.** `ApplyOptimisticLock(Database)` gives each row version the default value
   0 there (without it the INSERT fails: the column is NOT NULL and nobody gives it a value) and logs ONE
   warning through the application's logger (`Database.GetService<ILoggerFactory>()`, category
   `Zerox.Ef.Db`): the lock is off, every save goes through.
5. **A client version is a number.** `?version=N` binds a `ulong` and is converted to the row version's
   type; one that does not fit is a 400 (`TryDeleteById` returns false: an expected failure is a result,
   not an exception). A row version of another type (`byte[]` included) is refused with
   `NotSupportedException` when the model is built; on PostgreSQL only `uint` is accepted.
6. **An action that needs the current row** reads it, compares its version with the client's first and
   throws `StaleVersionException` (Zerox.Ef.Domain) → 409, before deciding anything.
7. **`LastWriterWins` keeps only the server's own changes** (re-read and re-apply on conflict); its client
   version goes: a client's one-field change is `Update(new T { Id, Field, Version }, [nameof(T.Field)])`.

Verified before planning (EF Core 10.0.13, Npgsql 10.0.3): on PostgreSQL `[Timestamp] uint` → `xmin`; the
version changes on a tracked save, on `ExecuteUpdate` and on raw SQL; an attached row with a stale
version, a version read before an `ExecuteUpdate`, a stale `Remove(stub)` all throw
`DbUpdateConcurrencyException`; marking the row version modified is ignored by EF without error. On
SQLite the INSERT fails without a default, works with `HasDefaultValue(0u)`, and the version stays 0.

The consumer CcV3 is adapted by its own plan after this one; this plan does not touch it.

## Design principles
- **KISS** — the lock is EF's (`[Timestamp]`, the provider's row version) plus three repository members;
  no counter, no trigger, no SQL of ours, no reload before a save.
- **DRY** — the version is generated in one place, the database; the editable fields are selected in one
  place, `EntityFields` (`Editable<T>()`); the 404/409 split after a failed save exists once,
  `EntityController<T>.SaveUnlessMissingAsync`, used by PUT, DELETE and a derived controller's action.
- **SRP** — `ModelBuilderExtensions` shapes the model (row versions, SQLite default, refused types);
  `Repository<T>` attaches and removes; the controller holds HTTP (404/409, 400); the middleware translates
  exceptions.
- **OCP** — a new locked entity is one `[Timestamp]` property; nothing in the library is edited. A new
  provider with a numeric row version needs no change.
- **LSP** — `Update`/`TryDeleteById` behave the same for every entity: with a row version the save is
  refused when stale; without one it writes. Entities without a row version keep working unchanged.
- **ISP** — `IRepository<T>` gains exactly what its consumers use: `Update`, `TryDeleteById`,
  `HasRowVersion`; `PinReadVersion` goes.
- **DIP** — Domain and Api depend on `IRepository<T>`; only `Repository<T>` and `ModelBuilderExtensions`
  read EF's model.

## Code rules
- Library code awaits with `ConfigureAwait(false)` (CA2007); everything async, no blocking wait, no
  synchronous road to the database — CLAUDE.md "Rules"
- No exception is hidden: an expected failure is a Try-pattern or a result (a failed save of a missing row
  → `SaveUnlessMissingAsync` false → 404; a version that does not fit → `TryDeleteById` false → 400); misuse is an
  `ArgumentException`; an unexpected exception is never caught to go on — CLAUDE.md "Rules" (P9)
- Optimistic lock: a client that sends the version it read never overwrites nor deletes a row changed after
  that read (409); only a change the server decides alone retries and lets the last writer win — CLAUDE.md
  "Rules"
- The public surface of `Zerox.Ef.*` is documented with `///` (CS1591 is an error there) — CLAUDE.md "Rules"
- `internal sealed` by default; `public` is a decision; constructor injection only — CLAUDE.md "Rules"
- Throw helpers at public members (`ArgumentNullException.ThrowIfNull`) before the first await —
  overlays/csharp/language.md §4
- Logging through `ILogger` with a constant template and named values; plain calls, no `[LoggerMessage]`
  (CA1848 is `none` for `src/Zerox.Ef/Api/**.cs` and, from block 01, `src/Zerox.Ef/Db/**.cs`) — CLAUDE.md,
  .editorconfig
- A public surface change updates `docs/catalog.md` (and `README.md` where it shows the use) in the same
  block — CLAUDE.md "Catalog"
- Tests: xUnit v3 + AwesomeAssertions, names `Should_<Outcome>_When<Condition>`, `async Task`, every async
  call takes `TestContext.Current.CancellationToken` (the `Ct` property of each test class),
  Arrange-Act-Assert with one Act — CLAUDE.md "Stack locks", overlays/csharp/testing.md
- A test proves the lock on PostgreSQL (`Assert.SkipUnless(TestDatabase.OnPostgres, …)`); the SQLite
  behaviour has its own tests (`Assert.SkipWhen(TestDatabase.OnPostgres, …)`); a test never asserts a
  concrete version number on PostgreSQL (`xmin` is a transaction id): it compares with the stored one —
  testing-principles.md 1.3
- Test objects come from the test projects' shared helpers (`TestDb`, `TestHost`, `SampleApp`, the
  entities in `TestDb.cs`/`TestHost.cs`); a version a test sends is the one it read from the database or
  the API, never a literal — testing-principles.md 5.1
- Everything in the repository is English; paths relative to the root — CLAUDE.md "Rules"

## Shared Capabilities
| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Row version in the model, SQLite default + warning, refused types | `src/Zerox.Ef/Db/ModelBuilderExtensions.cs::ApplyOptimisticLock(ModelBuilder, DatabaseFacade)` — created by 01 | 02, 04, 05 |
| Save a client's row / delete by id at the version read; has a row version | `src/Zerox.Ef/Core/IRepository.cs::Update`, `TryDeleteById`, `HasRowVersion` (impl. `src/Zerox.Ef/Db/Repository.cs`) — created by 02 | 04 |
| The fields a client may write | `src/Zerox.Ef/Domain/EntityFields.cs::Editable<T>()` — created by 03 | 04 |
| A stale version found by a read → 409 | `src/Zerox.Ef/Domain/StaleVersionException.cs` — created by 03; answered by `ErrorHandlingMiddleware` in 04 | 04 |
| 404 vs 409 after a failed save | `src/Zerox.Ef/Api/EntityController.cs::SaveUnlessMissingAsync` — created by 04 | 04 (PUT, DELETE, Sample `Rename`) |
| Captured log entries in tests | `tests/Zerox.Ef/LogCapture.cs::LogCapture` (moved from `TestHost.cs`, linked) — moved by 01 | 01, 04 |
| Sqlite/PostgreSQL test database | `tests/Zerox.Ef/TestDatabase.cs::TestDatabase` (`OnPostgres`) — EXISTS | 01, 02, 04, 05 |
| Optimistic concurrency, generated version read back | EF Core + Npgsql (`[Timestamp]`, `xmin`) — EXISTS, never re-implemented | 01–05 |

## Commands
- Gate (every block): `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan`
- Block tests on Sqlite: `dotnet test --project tests/Zerox.Ef/Db/Zerox.Ef.Db.Tests.csproj`,
  `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj`,
  `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` (the ones the block touches)
- Block tests on PostgreSQL (Docker running): `tools/test-postgres.sh` — from block 01 it passes its
  arguments to each `dotnet test`, so a focused run is `tools/test-postgres.sh --filter-class "*X*"`
- Fast regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Final gate: `tools/verify.sh && tools/test-postgres.sh`

## Execution Order
01-db-row-version          → no dependencies
02-db-client-row-writes    → 01
03-domain-editable-stale   → no dependencies
04-api-client-version      → 02, 03
05-remove-app-managed-lock → 04
</mission>

<files>
- 01-db.md
- 02-domain-api.md
</files>
