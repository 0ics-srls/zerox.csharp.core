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
| `Zerox.Ef.Api` | ASP.NET Core: `EntityController<T>`, error middleware (400/409/500), JSON as is | `Zerox.Ef.Domain`, ASP.NET Core |

What each project offers, by need: [docs/catalog.md](docs/catalog.md).

## Building on the database foundation

The `Zerox.Ef` family: `Zerox.Ef.Core` holds the contracts, `Zerox.Ef.Db` the EF Core side,
`Zerox.Ef.Domain` the search and `Zerox.Ef.Api` the controller: an application takes from each its
piece. The complete example, tested from the repository to HTTP, is
[`tests/Zerox.Ef/Api/Sample`](tests/Zerox.Ef/Api/Sample)
(`Core` the app's own contracts, `Db` entities and context, `Domain` services, `Api` controllers and host).

1. Entities implement `IEntity` (and `IHasOptimisticLock` for the opt-in lock):

       public sealed class Customer : IEntity, IHasOptimisticLock
       {
           public int Id { get; set; }
           public string Name { get; set; } = "";
           public int Version { get; set; }
       }

2. The context adds `IDbContext` and nothing else; `ApplyOptimisticLock` ends the model:

       public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options), IDbContext
       {
           public DbSet<Customer> Customers => Set<Customer>();
           protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock();
       }

3. Register it with the session rules and the repositories:

       builder.Services.AddZeroxDb<SampleDbContext>(o => o.UseSqlite(connection));

4. Controllers with the JSON as is, the OpenAPI document clients are generated from, and the error
   middleware first; in Development the document and the Scalar UI (the project's package
   `Scalar.AspNetCore`):

       builder.Services.AddControllers().AddZeroxJson();
       builder.Services.AddZeroxOpenApi();
       app.UseZeroxErrorHandling();
       app.MapControllers();
       if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.MapScalarApiReference(); }

5. One line per entity — the project adds its own `[Authorize]`:

       public sealed class CustomersController(IRepository<Customer> repository) : EntityController<Customer>(repository);

   It answers `POST api/Customers/load` (DevExtreme load options, server-side: the options decide
   rows, columns or groups),
   `GET api/Customers/values/{column}`, and the CRUD. A service that writes more than one repository
   opens `IUnitOfWork` (`CustomerMerge`); many rows without loading them go through
   `IMassiveOpHandler<T>` (`OrdersController.CloseBefore`); a single-field action uses `LastWriterWins`
   (`CustomersController.Rename`: with the Version the client read, a stale one answers 409). A row
   with `IHasOptimisticLock` is deleted with the Version the client read: `DELETE api/Customers/5?version=3`.

6. A TypeScript client is generated from the document with Orval, and its load is typed from the
   request: `tests/Zerox.Ef/Api/Sample/client/` (`src/load.ts`, run by `ClientTests` against the Sample).
   The document is a file of the repository, `openapi/Sample.json`, kept equal to the served one by
   `OpenApiDocumentTests`.

## Build and test

    dotnet build zerox.csharp.core.slnx -warnaserror
    dotnet test --solution zerox.csharp.core.slnx
    tools/verify.sh        # every check, [k/4]
    tools/test-postgres.sh # the Zerox.Ef tests on PostgreSQL in Docker, [k/5]

The integration tests include `ClientTests`, which needs Node 22.18 or later on the PATH and the npm
registry.

Requires the .NET SDK 10.0.400 (see `global.json`).
