# zerox.csharp.core

The reusable C# core of 0ics.

| Project | What it holds | Depends on |
|---|---|---|
| `Zerox.Core` | concurrency, cancellation, collections, realtime statistics, messaging, reflection, extensions | the framework |
| `Zerox.Core.Avalonia` | throttled property notifications, async commands | `Zerox.Core`, Avalonia 12.1.4 |
| `Zerox.Dnf` | boolean rule compiler (expression → disjunctive normal form → masks) | nothing |
| `Zerox.Ef.Core` | the contracts of the database foundation: entity, repository, unit of work, massive operation, optimistic lock | nothing |
| `Zerox.Ef.Db` | the database foundation on EF Core: `IDbContext`, repositories, session rules on save | `Zerox.Ef.Core`, EF Core Relational 10 |
| `Zerox.Ef.Domain` | the DevExtreme search over an `IQueryable`: rows, groups, projection, validator, distinct values, last writer wins | `Zerox.Ef.Db`, DevExtreme.AspNet.Data 5.1 |
| `Zerox.Ef.Api` | ASP.NET Core: `EntityController<T>`, one error format (ProblemDetails with the code of the domain), JSON as is, the OpenAPI document | `Zerox.Ef.Domain`, ASP.NET Core |

What each project offers, by need: [docs/catalog.md](docs/catalog.md).

## Building on the database foundation

The `Zerox.Ef` family: `Zerox.Ef.Core` holds the contracts, `Zerox.Ef.Db` the EF Core side,
`Zerox.Ef.Domain` the search and `Zerox.Ef.Api` the controller: an application takes from each its
piece. The complete example, tested from the repository to HTTP, is
[`tests/Zerox.Ef/Api/Sample`](tests/Zerox.Ef/Api/Sample)
(`Core` the app's own contracts, `Db` entities and context, `Domain` services, `Api` controllers and host).

1. Entities implement `IEntity`; the opt-in optimistic lock is EF's `[Timestamp]` on the row version the
   database generates — `uint` on PostgreSQL (the system column `xmin`), `ulong` on SQL Server
   (`rowversion`). Nothing of ours writes it: EF reads it back after every save. SQLite has no row
   version: there the lock is off, and a warning says so at startup.

       public sealed class Customer : IEntity
       {
           public int Id { get; set; }
           public string Name { get; set; } = "";
           [Timestamp]
           public uint Version { get; set; }
       }

2. The context adds `IDbContext` and nothing else; `ApplyOptimisticLock(Database)` ends the model:

       public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options), IDbContext
       {
           public DbSet<Customer> Customers => Set<Customer>();
           protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock(Database);
       }

3. Register it with the session rules and the repositories, then an entity service for every entity
   (`EntityService<T>`: the search and the CRUD as results, for the API and an in-process UI alike):

       builder.Services.AddZeroxDb<SampleDbContext>(o => o.UseSqlite(connection)).AddZeroxDomain();

   An entity with operations of its own derives the service and registers it under its own contract
   and `IEntityService<T>`:

       public sealed class CustomerService(IRepository<Customer> customers) : EntityService<Customer>(customers), ICustomerService { … }
       builder.Services.AddEntityService<Customer, ICustomerService, CustomerService>();

4. Controllers with the JSON as is, the OpenAPI document clients are generated from, and the error
   middleware first; in Development the document and the Scalar UI (the project's package
   `Scalar.AspNetCore`):

       builder.Services.AddControllers().AddZeroxJson();
       builder.Services.AddZeroxProblemDetails();
       builder.Services.AddZeroxOpenApi();
       app.UseZeroxErrorHandling();
       app.MapControllers();
       if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.MapScalarApiReference(); }

5. One line per entity, over its service — the controller never touches the storage, and the project adds
   its own `[Authorize]`:

       public sealed class OrdersController(IEntityService<Order> service) : EntityController<Order>(service);
       public sealed class CustomersController(ICustomerService customers) : EntityController<Customer>(customers) { … }

   It answers `POST api/Customers/load` (`LoadOptions`, DevExtreme's load options member by member,
   server-side: the options decide rows, columns or groups),
   `POST api/Customers/values/{column}` (the load options in the body, as the load: only their `Filter`
   is read — a dropdown shows each value with the number of rows that have it; the body may be left
   out), and the CRUD. An entity's own operations live in its service, each
   run through `RunAsync` and answered by the controller with `Answer(...)`: one that writes more than one
   repository opens `IUnitOfWork` (`CustomerService.MergeIntoAsync`); many rows without loading them go
   through `IMassiveOpHandler<T>` (`OrderService.CloseBeforeAsync`); a single-field change writes the client's
   one field at the version it read, without reading the row (`CustomerService.RenameAsync`:
   `Repository.Update(new Customer { Id, Name, Version }, [nameof(Customer.Name)])`, a stale one is a Conflict,
   409).
   A row with a row version is deleted with the version the client read: `DELETE api/Customers/5?version=3`.
   A controller that is not the controller of an entity — an operation of its own, a composite answer —
   derives `ResultController`: it gets the same `Answer(...)` and nothing else (no action, no route), and
   writes its own:

       [Route("api/Reports")]
       public sealed class ReportsController(IReports reports) : ResultController
       {
           [HttpGet("{year:int}")]
           public async Task<ActionResult<YearReport>> Year(int year, CancellationToken ct) => Answer(await reports.YearAsync(year, ct));
       }

   **Breaking (feature/16-values-on-load-options)** for a project on an earlier version: the values
   endpoint is `POST api/<Entity>/values/{column}` with the load options in the body (the `GET` is
   gone), `IEntityService<T>.ValuesAsync` takes `(column, LoadOptions, ct)` and answers
   `TextValue` (`Value`, `Count`) instead of a string — the values follow the service's `SearchSource()`
   and the options' filter, so a derived service that overrode `ValuesAsync` only to narrow its rows can
   drop the override.

   **Breaking (feature/11-entity-service)** for a project on an earlier version: `EntityController<T>` takes
   `IEntityService<T>` (call `AddZeroxDomain()` after `AddZeroxDb`), not `IRepository<T>`; a derived action
   calls its service and answers with `Answer(...)` instead of `Repository` / `SaveUnlessMissingAsync` (both
   live in `EntityService<T>` now); the load body is `LoadOptions`, so a generated client renames
   `DataSourceLoadOptionsBase` (the JSON is the same).

6. A TypeScript client is generated from the document with Orval, and its load is typed from the
   request: `tests/Zerox.Ef/Api/Sample/client/` (`src/load.ts`, no type assertion; checked by `tsc`, ESLint with
   every rule on except the ones decided off, Prettier; run with Vitest by `ClientTests` against the Sample).
   The document is a file of the repository, `openapi/Sample.json`, kept equal to the served one by
   `OpenApiDocumentTests`.

## Use it from a project

The library is consumed as a git submodule, by project references:

    git submodule add https://github.com/0ics-srls/zerox.csharp.core.git externals/zerox.csharp.core
    git -C externals/zerox.csharp.core checkout v1.0.0

A release is a tag `v<major>.<minor>.<patch>` on `main`; what each one changes is in [CHANGELOG.md](CHANGELOG.md).

```xml
<ProjectReference Include="../../externals/zerox.csharp.core/src/Zerox.Ef/Api/Zerox.Ef.Api.csproj" />
```

Reference the projects of the table above that you need: each brings the ones it depends on. Add nothing around the
submodule: its own `global.json`, `Directory.Build.props`, `Directory.Packages.props` and `.editorconfig` keep your
project's settings out of it, whichever they are. It requires the .NET 10 SDK. What it offers, by need, is in
`docs/catalog.md`; how a service is put together is in the steps above.

## Build and test

    dotnet build zerox.csharp.core.slnx -warnaserror
    dotnet test --solution zerox.csharp.core.slnx
    tools/verify.sh        # every check, [k/4]
    tools/test-postgres.sh # the Zerox.Ef tests on PostgreSQL in Docker, [k/5]

The integration tests include `ClientTests`, which needs Node 22.18 or later on the PATH and the npm
registry.

Requires the .NET SDK 10.0.400 (see `global.json`).
