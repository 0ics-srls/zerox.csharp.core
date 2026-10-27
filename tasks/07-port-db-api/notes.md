# 07 — port the DB → API foundation

## Request

©User 2026-10-27: build into zerox.csharp.core the foundation every project with a database can build
on — context, repository, unit of work, controller — the one unified in Filo, so other projects can use
it. Decisions taken in the conversation:

- layers Core → Db → Domain → Api, all inside this repository;
- **nothing of Filo stays in the library**: no names, no domain hooks, no Filo concepts (Filo is only
  the source, and Filo itself is not changed);
- **everything rests on an `IDbContext`**: no base class to extend; a project writes
  `class MyContext : DbContext, IDbContext`;
- **no authorization in the base controller**: the project puts its own `[Authorize]`;
- **no tags**: `ITag` / `ITagRepository` are not ported (a Filo workaround); one port only,
  `IRepository<T> where T : IEntity`;
- out of scope for now: MCP, API key, test infrastructure library, login and roles;
- **a complete example in the tests**: a sample application with tests at every level, up to
  end-to-end over HTTP.

## Source (read-only)

Filo, the working copy at `../../home/laco/Gestionale` from this clone (written `<filo>` below). Never
edit it. "Port" means: the capability and its behaviour come over, every name and comment is rewritten
in English and without Filo concepts, and the tests come over with it (renamed `Should_…_When…`).

## Code Organization

zerox gives every layer of a project its piece; the chain Core → Db → Domain → Api is the project's
(©User 2026-10-27: *"zerox fornisce a tutti un po, poi nel progetto c'è la Core che fornisce Db che
fornisce Domain che fornisce Api"*).

| Project | Holds | Depends on |
|---|---|---|
| `src/Zerox.Core` (exists) | + `Data/`: the contracts only — `IEntity`, `IHasOptimisticLock`, `IRepository<T>`, `IRepositoryFactory`, `IUnitOfWork`, `IMassiveOpHandler<T>`, `IFieldsToSet<T>` (no EF type in any) | nothing |
| `src/Zerox.Db` | `IDbContext` (it names EF types) and the EF Core implementation of the Core contracts: session + interceptor, repository, unit of work, massive operation, optimistic lock, `AddZeroxDb<TContext>` | `Zerox.Core`, Microsoft.EntityFrameworkCore.Relational |
| `src/Zerox.Domain` | the DevExtreme search over an `IQueryable` (rows, groups, projection, safe options, reserved fields), the options validator, distinct text values, last-writer-wins, `InvalidRequestException` | `Zerox.Db`, DevExtreme.AspNet.Data |
| `src/Zerox.Api` | `EntityController<T>`, the error middleware (`ApiError`, 400/409/500), JSON "as is" with enum-as-string and `DateOnly` converters | `Zerox.Domain`, ASP.NET Core (framework reference) |
| `tests/Zerox.Db.Tests`, `tests/Zerox.Domain.Tests`, `tests/Zerox.Api.Tests` | the tests of each project; `tests/Zerox.Api.Tests/Sample` is **the example application** | the project under test, Sqlite in memory |

`Zerox.Core` stays without dependencies: assetforge keeps referencing only `Zerox.Core` and does not
pull EF or ASP.NET; a project's entity layer references `Zerox.Core` for `IEntity` and stays free of EF.

### The mechanism: state outside the context

In Filo the state of the boundary and of the massive operation lives inside the sealed
`FiloDbContext`, which overrides `SaveChangesAsync`. With an interface there is no override, so:

- `DbSession` (internal) holds the state of ONE context instance: the open boundary (root), the open
  massive operation, its transaction, the "applying" flag. It is attached to the context instance
  through a `ConditionalWeakTable<object, DbSession>` (`DbSession.Of(context)`): no DI registration,
  it lives and dies with the context;
- `SessionInterceptor` (internal, ONE singleton instance) is a `SaveChangesInterceptor` added to the
  context options by `UseZeroxSession()` (called by `AddZeroxDb`). A singleton instance keeps the
  options identical across scopes, so EF does not build a new internal service provider per scope.
  On every save it reads `DbSession.Of(eventData.Context)` and:
  - sync save → `NotSupportedException` (the sync door would bypass the boundary);
  - massive operation open → `InvalidOperationException`;
  - boundary open and not applying → `InterceptionResult<int>.SuppressWithResult(0)` (swallowed);
  - otherwise → `Version++` on every `Modified` `IHasOptimisticLock`, then the save goes through;
- the project's own rules before saving (in Filo: two derived fields) go in the project's own
  `SaveChangesInterceptor`: the library has no hook;
- `Repository<T>` refuses a context configured without `UseZeroxSession()`
  (`InvalidOperationException`): without the interceptor the boundary would silently not swallow.

## Inventory — capabilities and where they already exist

Searched in this repository (LSAI not available: SDK 10.0.400 missing on the machine where the plan
was written; grep, low confidence, on `src/`): no data access, no EF, no ASP.NET, no DevExtreme
anywhere. Every capability below is new to zerox; its source is Filo.

| Capability | Source in Filo | Target |
|---|---|---|
| entity with a key | `<filo>/src/Filo.Core/Entities/IEntity.cs` | `Zerox.Core/Data/IEntity.cs` |
| port to a table | `<filo>/src/Filo.Core/Interfaces/IRepositoryBase.cs` + `IRepository.cs` (merged, tags dropped) | `Zerox.Db/IRepository.cs` |
| repository of a type decided at run time | `<filo>/src/Filo.Core/Interfaces/IRepositoryFactory.cs` | `Zerox.Db/IRepositoryFactory.cs` |
| the boundary | `<filo>/src/Filo.Core/Interfaces/IUnitOfWork.cs`, `<filo>/src/Filo.Infrastructure/Data/UnitOfWork.cs`, `FiloDbContext.cs` lines 31–153 | `Zerox.Db/IUnitOfWork.cs`, `UnitOfWork.cs`, `DbSession.cs`, `SessionInterceptor.cs` |
| massive update/delete | `<filo>/src/Filo.Core/Interfaces/IMassiveOpHandler.cs`, `ICampiDaScrivere.cs`, `<filo>/src/Filo.Infrastructure/Data/MassiveOpHandler.cs` | `Zerox.Db/IMassiveOpHandler.cs`, `IFieldsToSet.cs`, `MassiveOpHandler.cs` |
| optimistic lock | `<filo>/src/Filo.Core/Interfaces/IHasOptimisticLock.cs`, `<filo>/src/Filo.Infrastructure/Data/OptimisticLockExtensions.cs` | `Zerox.Core/Data/IHasOptimisticLock.cs`, `Zerox.Db/ModelBuilderExtensions.cs`, `SessionInterceptor.cs` |
| repository implementation | `<filo>/src/Filo.Infrastructure/Repositories/RepositoryBase.cs`, `Repository.cs`, `RepositoryFactory.cs` | `Zerox.Db/Repository.cs`, `RepositoryFactory.cs` |
| search rows/groups/projection, safe options, reserved fields | `<filo>/src/Filo.Server/Controllers/RicercaDevExtreme.cs`, `<filo>/src/Filo.Core/Models/RisultatoGruppi.cs`, `<filo>/src/Filo.Core/Models/DataSourceResult.cs` | `Zerox.Domain/Search.cs`, `GroupsResult.cs`, `DataSourceResult.cs` |
| options validator | `<filo>/src/Filo.Server/Controllers/ValidatoreRichiesta.cs` | `Zerox.Domain/LoadOptionsValidator.cs` |
| distinct text values | `<filo>/src/Filo.Server/Controllers/ColonneDiTesto.cs` | `Zerox.Domain/DistinctTextValues.cs` |
| invalid request | `<filo>/src/Filo.Core/Exceptions/RichiestaNonValidaException.cs` | `Zerox.Domain/InvalidRequestException.cs` |
| last writer wins | `<filo>/src/Cartellino.Server/Controllers/CartellinoControllerBase.cs` `SaveWithLastWriterWinsAsync` | `Zerox.Domain/LastWriterWins.cs` |
| error contract + middleware | `<filo>/src/Filo.Core/Errors/AppError.cs`, `<filo>/src/Filo.Web/Middleware/ErrorHandlingMiddleware.cs` | `Zerox.Api/ApiError.cs`, `ErrorHandlingMiddleware.cs` |
| JSON as is + DateOnly | `<filo>/src/Filo.Web/FiloWebExtensions.cs` (`AddFiloWebJson`, `AggiungiConvertitori`), `<filo>/src/Filo.Web/Configuration/DateOnlyJsonConverter.cs` | `Zerox.Api/ZeroxJson.cs`, `DateOnlyJsonConverter.cs` |
| base controller | `<filo>/src/Filo.Server/Controllers/BaseController.cs` (no `[Authorize]`, no MCP attributes) | `Zerox.Api/EntityController.cs` |

Tests ported (source → target):

| Filo test | Target |
|---|---|
| `<filo>/tests/Filo.Infrastructure.Tests/Data/RepositoryTests.cs` | `tests/Zerox.Db.Tests/RepositoryTests.cs` |
| `<filo>/tests/Filo.Infrastructure.Tests/Data/ConfineDiScritturaTests.cs` | `tests/Zerox.Db.Tests/UnitOfWorkTests.cs` |
| `<filo>/tests/Filo.Infrastructure.Tests/Data/ScrittureMassiveTests.cs` | `tests/Zerox.Db.Tests/MassiveOpTests.cs` |
| `<filo>/tests/Filo.Infrastructure.Tests/Data/OptimisticLockConfigTests.cs`, `Repositories/StaccareTutteLeRigheTests.cs` | `tests/Zerox.Db.Tests/OptimisticLockTests.cs` |
| `<filo>/tests/Filo.Server.Tests/Batteria/OpzioniSicureTests.cs`, `Controllers/ValidatoreRichiestaTests.cs` | `tests/Zerox.Domain.Tests/LoadOptionsValidatorTests.cs` |
| `<filo>/tests/Filo.Server.Tests/Controllers/BaseControllerValoriDistintiTests.cs` (the domain half) | `tests/Zerox.Domain.Tests/SearchTests.cs` |
| `<filo>/tests/Cartellino.Server.Tests/Controllers/OptimisticLockRetryTests.cs` | `tests/Zerox.Domain.Tests/LastWriterWinsTests.cs` |
| `<filo>/tests/Filo.Server.Tests/Middleware/ErrorHandlingConcurrencyTests.cs` | `tests/Zerox.Api.Tests/ErrorHandlingTests.cs` |
| `<filo>/tests/Filo.Server.Tests/Controllers/BaseControllerQueryTests.cs`, `BaseControllerValoriDistintiTests.cs` | `tests/Zerox.Api.Tests/Sample/…` (end-to-end over HTTP) |

## The example (the sample application in the tests)

`tests/Zerox.Api.Tests/Sample` is what a new project writes, and nothing more, organised by the project's
layers (`Sample/Core` → `Sample/Db` → `Sample/Domain` → `Sample/Api`, each using only the ones before).
`Zerox.Core` holds zerox's contracts; the app's own contracts go in the app's Core (©User: *"se app ha
suoi contratti deve avere core"*):

- `Sample/Core`: the app's contracts only — `ICustomerMerge`;

- `Sample/Db`: entities `Customer` (`IEntity`, `IHasOptimisticLock`: `Id`, `Name`, `City`, `Version`, `Orders`) and
  `Order` (`IEntity`: `Id`, `CustomerId`, `Customer`, `Date` `DateOnly`, `Amount` decimal, `Status`
  enum `OrderStatus {Open, Closed}`, `InternalNote` with `[JsonIgnore]` as a reserved field) and
  `SampleDbContext : DbContext, IDbContext` with `modelBuilder.ApplyOptimisticLock()`;
- `Sample/Api`: `CustomersController : EntityController<Customer>` and `OrdersController : EntityController<Order>`
  with the sample's own `[Authorize]`-free routes;
- `Sample/Domain`: `CustomerMerge : ICustomerMerge` — a service that moves every order of one customer to another and deletes the first,
  across two repositories in ONE boundary: the example of the unit of work;
- `CustomersController.Rename` — `PUT api/Customers/{id}/name`, a single-field action through
  `LastWriterWins`: the example of the optimistic lock;
- `SampleApp` — builds a real `WebApplication` on `TestServer` with Sqlite in memory:
  `AddZeroxDb<SampleDbContext>`, `AddControllers().AddZeroxJson()`, `UseZeroxErrorHandling()`.

Tests at every level on the sample: repository (Db), service with the boundary (Domain), and end-to-end
HTTP (Api).
