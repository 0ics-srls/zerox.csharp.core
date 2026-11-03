# TDDAB Plan: The service layer — EntityService<T>, OpResult<T>, contracts in Core
**Date:** 2026-11-03

<mission>
## The project

**zerox.csharp.core** is the reusable C# core of 0ics: `Zerox.Core`, `Zerox.Core.Avalonia`, `Zerox.Dnf`
and the **Zerox.Ef** family, the database → API foundation of applications. .NET 10 (SDK 10.0.400, C# 14),
solution `zerox.csharp.core.slnx`, Central Package Management (`Directory.Packages.props`, no version in a
`.csproj`). Branch `feature/11-entity-service` (from `feature/10-db-row-version`). Paths below are relative
to the repository root. Every project sees its tests' internals (`Directory.Build.targets`:
`InternalsVisibleTo $(MSBuildProjectName).Tests`). Analyzers: `latest-recommended` as errors; the public
surface of `Zerox.Ef.*` must carry `///` (CS1591 is an error).

The Zerox.Ef family, one way only — `Zerox.Ef.Core` → `Zerox.Ef.Db` → `Zerox.Ef.Domain` → `Zerox.Ef.Api`:
```
src/Zerox.Ef/Core/     contracts, NO dependency (BCL only): IEntity (int Id), IRepository<T>, IRepositoryFactory,
                       IUnitOfWork, IMassiveOpHandler<T>, IFieldsToSet<T>
src/Zerox.Ef/Db/       IDbContext + EF implementation: Repository<T>, SessionInterceptor, UnitOfWork,
                       MassiveOpHandler, ModelBuilderExtensions.ApplyOptimisticLock, ServiceCollectionExtensions.AddZeroxDb
src/Zerox.Ef/Domain/   (package DevExtreme.AspNet.Data) InvalidRequestException, StaleVersionException,
                       Search (LoadAsync on DataSourceLoadOptionsBase → DevExtreme LoadResult; DefaultMaxTake 1000),
                       LoadOptionsValidator, DistinctTextValues (IsTextColumn<T>, ValuesAsync), EntityFields.Editable<T>(),
                       EntityPaths, LastWriterWins
src/Zerox.Ef/Api/      EntityController<T>, ErrorHandlingMiddleware (UseZeroxErrorHandling), ApiErrorCode,
                       ZeroxProblemDetails (Code from the status: 400 InvalidRequest, 404 NotFound, 409 Conflict,
                       500 InternalError), ZeroxJson (names as declared), ZeroxOpenApi (LoadResults, UnusedLoadResult, …)
tests/Zerox.Ef/Domain/ Zerox.Ef.Domain.Tests — TestDb.cs (Item, Box, Memo [Timestamp] uint, TestDbContext, TestDb:
                       CreateAsync, NewContext(), NewScope() = a scope of AddZeroxDb<TestDbContext>), SearchTests, SurfaceTests
tests/Zerox.Ef/Api/    Zerox.Ef.Api.Tests — TestHost.cs (Doc [Timestamp], Plain, HostDbContext, TestHost.StartAsync(map)),
                       ErrorHandlingTests, Sample/ (the example application: Core/, Db/Customer+Order+SampleDbContext,
                       Domain/, Api/CustomersController+OrdersController+RenameRequest+SampleApp, openapi/Sample.json,
                       client/ = TypeScript client generated from Sample.json, its src/load.ts and src/sample.test.ts tracked)
tests/Zerox.Ef/TestDatabase.cs  linked into the three Ef test projects (Sqlite in memory; PostgreSQL when
                       ZEROX_EF_TEST_POSTGRES is set — tools/test-postgres.sh; TestDatabase.OnPostgres)
```

## What this plan changes, and why

Zerox.Ef has no service layer: `EntityController<T>` receives `IRepository<T>` and itself runs the search,
the CRUD and the 404/409 split (`SaveUnlessMissingAsync`) — the transport talks to the storage
(`branches/service-layers.md` 3). An in-process UI (Avalonia) can reuse nothing, and an entity's own logic
has no place (CcV3 grew `ITaskItemTriggers` beside the controller). Expected failures are exceptions
(`InvalidRequestException`, `StaleVersionException`, `DbUpdateConcurrencyException`) only the HTTP middleware
translates.

The owner's design (interview 2026-10-31; `tasks/11-entity-service/notes.md`):
1. **`OpResult<T>`** in `Zerox.Ef.Core`: the outcome of an operation — a status, the value, the reason, and
   the exception inside when one happened, so the caller never gets an exception. Statuses
   **Ok, Created, Deleted, NotFound, Conflict, Invalid, Failed, Faulted**. No HTTP meaning: `Failed` is "the
   domain refused it" (a trigger the state does not accept); `Faulted` carries **every** exception,
   cancellation included.
2. **The contracts in `Zerox.Ef.Core`** with **own load types** (no DevExtreme in Core): `LoadOptions`
   copies **every** member of DevExtreme's `DataSourceLoadOptionsBase` with the same names (the wire does not
   change), `SortingInfo`, `GroupingInfo`, `SummaryInfo` (DevExtreme's names), `Loaded` (`data`,
   `totalCount`, `groupCount`, `summary` — DevExtreme's JSON names) and `LoadGroup` (`key`, `items`,
   `count`, `summary`). `IEntityService<T>`: load, distinct values, get, create, update at the client's
   version, delete at the client's version.
3. **`EntityService<T>` in `Zerox.Ef.Domain`**, public and derivable, over `IRepository<T>`; every operation —
   the base's and a derived service's — runs through **one** `RunAsync`, the only place where an exception
   becomes a status. `AddZeroxDomain()` registers it for every entity; `AddEntityService<T, TContract,
   TService>()` registers a derived service under its own contract and `IEntityService<T>`.
4. **`EntityController<T>` over `IEntityService<T>`**, never a repository; one mapping status → HTTP:
   Ok 200, Created 201, Deleted 204, NotFound 404, Conflict 409, Invalid 400, **Failed 400**, Faulted → the
   captured exception rethrown to `ErrorHandlingMiddleware`, which already logs it, answers 500, treats a
   gone client as Information and aborts a started response. The problem `Code` stays the status's
   (`ZeroxProblemDetails.CodeOf`): a Failed answer reads `InvalidRequest`, its detail says why.
5. **The Sample** shows it: `CustomerService` (merge, rename) and `OrderService` (close before a date) derive
   `EntityService<T>`; the controllers call them.

No generic state-machine API (the owner: firing on the server is a case's own logic, in that entity's
service). The consumer CcV3 is adapted by its own plan after this one; this plan does not touch it.

## Design principles
- **KISS** — one result record with a closed set of statuses; one service class; one mapping per side; the
  load types are plain classes copying DevExtreme's members by name.
- **DRY** — exception → status exists once (`EntityService<T>.RunAsync`), status → HTTP once
  (`EntityController<T>.Answer`), the 404/409 split once (`EntityService<T>.SaveUnlessMissingAsync`), the
  optimistic-lock test once (`OptimisticLockConflict.Is`, used by the service and the middleware); the
  search stays `Search.LoadAsync`, the values `DistinctTextValues`, the editable fields
  `EntityFields.Editable<T>()`.
- **SRP** — Core declares; `LoadConversion` converts between the own types and DevExtreme's; the service
  holds the operations; the controller holds HTTP; the middleware holds exceptions that reach the top.
- **OCP** — a new entity is served by the open-generic registration; an entity's own operation is a
  method of its derived service run through `RunAsync`; a new status is one enum member, and the
  mapping's test turns red until it is mapped.
- **LSP** — a derived service is an `IEntityService<T>`: the base operations keep their contract (never
  throw, the same statuses); a derived controller receives its derived service as the base's
  `IEntityService<T>`.
- **ISP** — `IEntityService<T>` holds exactly what `EntityController<T>` and an in-process UI call;
  an entity's own operations go in its own contract (`ICustomerService : IEntityService<Customer>`).
- **DIP** — Api depends on `IEntityService<T>` (Core); Domain implements it; Core references nothing.

## Code rules
- Library code awaits with `ConfigureAwait(false)` (CA2007); everything async, no blocking wait — CLAUDE.md "Rules"
- No exception is hidden: the service returns an expected failure as a status and carries an unexpected one
  inside `Faulted`; misuse of a public member is an `ArgumentException` thrown before the first await; the
  API rethrows a `Faulted` exception to the middleware, which logs it — CLAUDE.md "Rules" (P9), owner's
  decision 2026-10-31 ("all of them")
- Optimistic lock: a client that sends the version it read never overwrites nor deletes a row changed after
  that read (Conflict → 409) — CLAUDE.md "Rules"
- The public surface of `Zerox.Ef.*` is documented with `///` (CS1591 is an error there) — CLAUDE.md "Rules"
- `internal sealed` by default, `public` is a decision (the contracts, the result, the load types,
  `EntityService<T>` — derivable —, the registration); constructor injection only — CLAUDE.md "Rules"
- `Zerox.Ef.Core` keeps zero dependencies: BCL only (`System.Text.Json.Serialization` attributes are BCL) —
  owner's decision, notes Requirement 7
- Generic static factories live on a non-generic static class (`OpResult.Ok(value)`), never as static
  members of the generic type — CA1000
- Registration: open generic once, every other service one line, factory overload for a second contract of
  the same instance — overlays/csharp/aspnetcore.md §2
- Status mapping: a switch over the closed set whose last arm throws `UnreachableException`; a test runs
  `Enum.GetValues<OpStatus>()` through it — overlays/csharp/aspnetcore.md §3
- A public surface change updates `docs/catalog.md` (and `README.md` where it shows the use) in the same
  block — CLAUDE.md "Catalog"
- Tests: xUnit v3 + AwesomeAssertions, names `Should_<Outcome>_When<Condition>`, `async Task`, every async
  call takes `TestContext.Current.CancellationToken` (`Ct`), Arrange-Act-Assert with one Act — CLAUDE.md
  "Stack locks", overlays/csharp/testing.md
- A conflict is proved on PostgreSQL (`Assert.SkipUnless(TestDatabase.OnPostgres, "SQLite has no row version: the lock is proved on PostgreSQL.")`);
  a version a test sends is the one it read from the database or the API, never a literal —
  testing-principles.md 1.3, 5.1
- Everything in the repository is English; paths relative to the root — CLAUDE.md "Rules"

## Shared Capabilities
| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| The outcome of an operation | `src/Zerox.Ef/Core/OpResult.cs::OpStatus`, `OpResult<T>`, `OpResult` (factories) — created by 01 | 02, 03, 04, 05, 06 |
| Load request/result without DevExtreme | `src/Zerox.Ef/Core/LoadOptions.cs::LoadOptions`, `SortingInfo`, `GroupingInfo`, `SummaryInfo`; `src/Zerox.Ef/Core/Loaded.cs::Loaded`, `LoadGroup` — created by 01 | 02, 03, 06 |
| The service contract | `src/Zerox.Ef/Core/IEntityService.cs::IEntityService<T>` — created by 01 | 03, 04, 05, 06 |
| Own load types ↔ DevExtreme | `src/Zerox.Ef/Domain/LoadConversion.cs::LoadConversion.ToDevExtreme`, `ToLoaded` (internal) — created by 02 | 03 |
| Optimistic-lock conflict recognised | `src/Zerox.Ef/Domain/OptimisticLockConflict.cs::OptimisticLockConflict.Is` — created by 03 (moved from `ErrorHandlingMiddleware.IsOptimisticLockConflict`, which 06 removes) | 03, 06 |
| Exception → status; 404 vs 409 after a failed save | `src/Zerox.Ef/Domain/EntityService.cs::EntityService<T>.RunAsync`, `SaveUnlessMissingAsync` — created by 03 | 03, 05 |
| Domain registration | `src/Zerox.Ef/Domain/ServiceCollectionExtensions.cs::AddZeroxDomain`, `AddEntityService<T, TContract, TService>` — created by 04 | 05, 06 |
| Status → HTTP | `src/Zerox.Ef/Api/EntityController.cs::EntityController<T>.Answer` — created by 06 | 06 (base actions, Sample actions) |
| Search, values, editable fields, client-row writes | `Search.LoadAsync`, `DistinctTextValues`, `EntityFields.Editable<T>()`, `IRepository<T>.Update`/`TryDeleteById`/`HasRowVersion` — EXIST (inventory), never rewritten | 03 |
| Exception → HTTP at the top (log, 500, client gone, started response) | `src/Zerox.Ef/Api/ErrorHandlingMiddleware.cs` — EXISTS | 06 |
| Sqlite/PostgreSQL test database | `tests/Zerox.Ef/TestDatabase.cs::TestDatabase` (`OnPostgres`) — EXISTS | 02, 03, 05, 06 |

## Commands
- Gate (every block): `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan`
- Block tests on Sqlite: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj`,
  `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` (the ones the block touches)
- Block tests on PostgreSQL (Docker running): `tools/test-postgres.sh` (passes its arguments to each
  `dotnet test`: a focused run is `tools/test-postgres.sh --filter-class "*X*"`)
- Fast regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
- Final gate: `tools/verify.sh && tools/test-postgres.sh`

## Execution Order
01-core-contracts        → no dependencies
02-domain-load-conversion → 01
03-domain-entity-service → 01, 02
04-domain-registration   → 03
05-sample-services       → 03, 04
06-api-over-service      → 03, 04, 05
</mission>

<files>
- 01-core-domain.md
- 02-sample-api.md
</files>
