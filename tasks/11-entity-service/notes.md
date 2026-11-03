# 11 — The service layer: EntityService<T>, OpResult<T>, contracts in Core

## Requirements

From the owner's interview of 2026-10-31 (CcV3 redesign), the answers as given:

| # | Question | Answer |
|---|---|---|
| 1 | Where the logic of an entity lives | Zerox.Ef has no service layer: `EntityController<T>` receives `IRepository<T>` and holds search, CRUD, the version and 404/409. A generic **`EntityService<T>`** in Domain; the application derives its own (`TaskItemService : EntityService<TaskItem>`); the controller receives the **service, never the repository**; an Avalonia UI uses the same service without HTTP |
| 2 | How the service reports expected failures | a result that carries "state and data", **the exception inside too**, "so async is not broken and everything is clean", extensible later without changing the transport — named **`OpResult<T>`** |
| 3 | Its statuses | **Ok, Created, Deleted, NotFound, Conflict, Invalid, Failed, Faulted** |
| 4 | Which exceptions Faulted captures | **all of them** (cancellation included): the caller always gets an `OpResult` |
| 5 | A refused operation (a trigger the state does not accept) | at the service level it has **no HTTP meaning**: `Failed`. Only the controller translates to HTTP: **Failed → 400** |
| 6 | Where the contracts live | **`Zerox.Ef.Core`** |
| 7 | The load in a Core contract | **own types in Core** (the DevExtreme shape); Domain converts to DevExtreme; Core keeps zero dependencies |
| 8 | How an application gets and extends a service | generic base registered for every entity, derived services; **custom (overriding) services are registered too** |
| 9 | A generic state-machine API in Zerox.Ef | **none**: firing on the server is domain logic of a case, written in that entity's own service (CcV3's `TaskItemService` is the example) |

## Analysis

Read: `src/Zerox.Ef/{Core,Domain,Api}`, `docs/catalog.md`, the Sample (`tests/Zerox.Ef/Api/Sample`), the
consumer CcV3 (`src/CcV3.*/TaskItems`, `frontend/src/app/shared/load/load.types.ts`), the mindsets
`branches/service-layers.md` and `overlays/csharp/aspnetcore.md` §1-3.

- **The transport talks to the storage** — against `service-layers.md` 3 ("the transport never talks to
  the storage"): `EntityController<T>` (`src/Zerox.Ef/Api/EntityController.cs`) calls `Search.LoadAsync`,
  `DistinctTextValues`, `IRepository<T>` (`GetByIdAsync`, `AddAsync`, `Update`, `TryDeleteById`,
  `SaveChangesAsync`, `Query`) and tells 404 from 409 itself (`SaveUnlessMissingAsync`). The Sample's
  derived actions do the same (`CustomersController.Rename`, `OrdersController.CloseBefore`).
- **Expected failures are exceptions today**: `InvalidRequestException` (thrown by `Search`,
  `LoadOptionsValidator`, `EntityController`), `StaleVersionException`, `DbUpdateConcurrencyException`;
  `ErrorHandlingMiddleware` turns them into 400/409. An in-process caller (Avalonia) would have to catch
  them by type.
- **No Domain registration** exists: `Zerox.Ef.Db.ServiceCollectionExtensions.AddZeroxDb` registers
  `IRepository<>` and `IRepositoryFactory`; nothing registers a Domain type.
- **The load's wire shape** — request: `DataSourceLoadOptionsBase` bound from the body, PascalCase names
  (`Skip`, `Take`, `RequireTotalCount`, `RequireGroupCount`, `IsCountQuery`, `IsSummaryQuery`, `Filter`,
  `Sort`, `Group`, `Select`, `TotalSummary`, `GroupSummary`) plus the members the server forces or the
  client never sends (`PreSelect`, `RemoteSelect`, `RemoteGrouping`, `ExpandLinqSumType`, `PrimaryKey`,
  `DefaultSort`, `StringToLower`, `PaginateViaPrimaryKey`, `SortByPrimaryKey`, `AllowAsyncOverSync`).
  Response: DevExtreme's `LoadResult` (`data`, `totalCount`, `groupCount`, `summary`), documented by
  `ZeroxOpenApi.LoadResults` as `LoadResultOf<T>` with `LoadGroup`. CcV3's typed load
  (`load.types.ts` `LoadRequest<T>`) sends exactly the twelve client members.
- **Error codes** (`ApiErrorCode`: InvalidRequest, NotFound, Conflict, InternalError) are given by the
  status in one place (`ZeroxProblemDetails.CodeOf(int?)`).
- **Consumers to adapt**: the Sample (controllers, `CustomerMerge`, its TypeScript client and
  `openapi/Sample.json`) in this task; CcV3 in its own task afterwards (`TaskItemsController`,
  `ITaskItemTriggers`/`TaskItemTriggers` replaced by `TaskItemService`, the frontend's `body()` naming the
  regenerated request type). assetforge (submodule consumer) adapts when it moves its pointer:
  breaking change, recorded in the README.

## Inventory — capabilities and where they already live

Searched with LSAI (`zerox-csharp-core-c-3`, Ready 13/13): `Service`, `Result`, `Outcome`, `AddZeroxDb`,
`ServiceCollectionExtensions` by name; usages of `EntityController`, `InvalidRequestException`,
`StaleVersionException`.

| Capability | Where it lives | Decision |
|---|---|---|
| Search from load options (rows, Select, Group, summaries, page limit, reserved fields, 400 reasons) | `Zerox.Ef.Domain.Search.LoadAsync` | **reuse**: the service calls it after converting the options |
| Distinct values of a text column | `Zerox.Ef.Domain.DistinctTextValues` (`IsTextColumn<T>`, `ValuesAsync`) | **reuse** |
| Editable fields of a client row | `Zerox.Ef.Domain.EntityFields.Editable<T>()` | **reuse** |
| Save / delete at the client's version, has a row version | `IRepository<T>.Update`, `TryDeleteById`, `HasRowVersion` | **reuse** |
| 404 vs 409 after a failed save | `EntityController<T>.SaveUnlessMissingAsync` | **move** into the service (returns NotFound / Conflict instead of false / throw) |
| Optimistic-lock conflict recognised (a token on the entry) | `ErrorHandlingMiddleware.IsOptimisticLockConflict` | **move** to Domain, one copy: the service classifies with it; the middleware calls the same one |
| One write over several repositories | `IUnitOfWork` | **reuse** (Sample's `CustomerMerge` becomes a `CustomerService` operation) |
| Massive update | `IMassiveOpHandler<T>` | **reuse** (Sample's `CloseBefore` becomes an `OrderService` operation) |
| An operation's outcome as a value | none (`Service`, `Result`, `Outcome`: no type in Zerox.*) | **write**: `OpResult<T>` |
| A domain service per entity | none | **write**: `IEntityService<T>` / `EntityService<T>` |
| Domain registration | none (`AddZeroxDb` only) | **write**: `AddZeroxDomain`, `AddEntityService` |
| Load request / result types without DevExtreme | none | **write** in Core |
| Exception → HTTP (log + 500, client gone, response started) | `ErrorHandlingMiddleware` | **reuse**: a Faulted result is rethrown there (see Code Organization) |

## Code Organization

| Part | Owner | Shape |
|---|---|---|
| `OpStatus` + `OpResult<T>` | `Zerox.Ef.Core` | `enum OpStatus { Ok, Created, Deleted, NotFound, Conflict, Invalid, Failed, Faulted }`; `sealed record OpResult<T>(OpStatus Status, T? Value, string? Reason, Exception? Exception)` with factories `Ok(v)`, `Created(v)`, `Deleted()`, `NotFound()`, `Conflict(reason)`, `Invalid(reason)`, `Failed(reason)`, `Faulted(e)` and `IsSuccess`. No HTTP word anywhere |
| Load types | `Zerox.Ef.Core` | `LoadOptions` with **every** member of DevExtreme's `DataSourceLoadOptionsBase` (owner, PROPOSE 2026-11-03; names as on the wire today), `SortingInfo`, `GroupingInfo`, `SummaryInfo` (same names as DevExtreme's, so the generated client keeps them), `Filter` as `IList<object?>?`; `Loaded` (`data`, `totalCount`, `groupCount`, `summary`, JSON names as today) and `LoadGroup`. Plain BCL types |
| `IEntityService<T>` | `Zerox.Ef.Core` | `LoadAsync(LoadOptions)`, `ValuesAsync(column)`, `GetAsync(id)`, `CreateAsync(T)`, `UpdateAsync(T)`, `DeleteAsync(id, ulong? version)` — each `Task<OpResult<…>>` with a `CancellationToken`, never throws |
| `EntityService<T>` | `Zerox.Ef.Domain` | public, derivable, over `IRepository<T>`; `protected virtual MaxTake`, `SearchSource()`; **`protected RunAsync(Func<CancellationToken, Task<OpResult<R>>>)`** — the one place that turns exceptions into results: `InvalidRequestException` → Invalid, `StaleVersionException` and a token conflict → Conflict (NotFound when the row is gone), anything else → Faulted. Every base operation and every derived one runs through it |
| Options conversion | `Zerox.Ef.Domain` (internal) | `LoadOptions` → `DataSourceLoadOptionsBase`; the server-only members stay the server's (`Search.MakeSafe`) |
| `AddZeroxDomain()`, `AddEntityService<TContract, TService, T>()` | `Zerox.Ef.Domain` | open-generic `IEntityService<>` → `EntityService<>` (scoped); a derived service registered under its own contract **and** `IEntityService<T>`, one instance per scope |
| `EntityController<T>` | `Zerox.Ef.Api` | receives `IEntityService<T>` (protected `Service`), no repository; one mapping `OpResult` → HTTP: Ok 200, Created 201 + place, Deleted 204, NotFound 404, Conflict 409, Invalid 400, Failed 400, Faulted → the captured exception rethrown (`ExceptionDispatchInfo`) to `ErrorHandlingMiddleware`, which already logs it, answers 500, treats a gone client as Information and aborts a started response. The route/body id mismatch stays a 400 of the transport |
| Problem code | `Zerox.Ef.Api` | unchanged: the `Code` comes from the status (`ZeroxProblemDetails.CodeOf`), so a Failed answer reads `InvalidRequest` and its detail says why (owner, PROPOSE 2026-11-03) |
| OpenAPI | `Zerox.Ef.Api.ZeroxOpenApi` | the load documented as today (`LoadResultOf<T>`, `LoadGroup`); the request schema becomes `LoadOptions`; `Loaded` leaves the document like DevExtreme's `LoadResult` did |

Approved by the owner 2026-11-03 (Faulted rethrown to the middleware; Code from the status; LoadOptions
with every DevExtreme member). Plan: `tasks/11-entity-service/index.md` (TDDAB, 6 blocks).
| Sample | `tests/Zerox.Ef/Api/Sample` | `CustomerService : EntityService<Customer>` (`MergeIntoAsync`, `RenameAsync`) and `OrderService : EntityService<Order>` (`CloseBeforeAsync`), controllers over them, client regenerated |

Mechanism (code-principles §2): one result type with a closed set of statuses, and one translation per
side — `RunAsync` (exception → status) in Domain, the controller's mapping (status → HTTP) in Api, each a
switch over the closed set.

Removed: `EntityController<T>.SaveUnlessMissingAsync` and `Repository` (protected), `ICustomerMerge` /
`CustomerMerge` of the Sample (become `CustomerService` operations).

## Out of scope

- A generic state-machine API (Requirement 9).
- CcV3's adaptation: its own task after this one — `TaskItemService : EntityService<TaskItem>` with the
  server fire as an example, the TypeScript client machine, `ITaskItemTriggers` removed.
