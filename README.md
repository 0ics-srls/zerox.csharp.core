# zerox.csharp.core

The reusable C# core of 0ics.

| Project | What it holds | Depends on |
|---|---|---|
| `Zerox.Core` | concurrency, cancellation, collections, realtime statistics, messaging, reflection, extensions, data contracts (entity, repository) | the framework |
| `Zerox.Core.Avalonia` | throttled property notifications, async commands | `Zerox.Core`, Avalonia 12.1.4 |
| `Zerox.Dnf` | boolean rule compiler (expression → disjunctive normal form → masks) | nothing |
| `Zerox.Db` | the database foundation on EF Core: `IDbContext`, repositories, session rules on save | `Zerox.Core`, EF Core Relational 10 |
| `Zerox.Domain` | the DevExtreme search over an `IQueryable`: rows, groups, projection, validator, distinct values, last writer wins | `Zerox.Db`, DevExtreme.AspNet.Data 5.1 |
| `Zerox.Api` | ASP.NET Core: `EntityController<T>`, error middleware (400/409/500), JSON as is | `Zerox.Domain`, ASP.NET Core |

What each project offers, by need: [docs/catalog.md](docs/catalog.md).

## Building on the database foundation

`Zerox.Core` holds the contracts, `Zerox.Db` the EF Core side, `Zerox.Domain` the search and
`Zerox.Api` the controller: an application takes from each its piece. The complete example, tested
from the repository to HTTP, is [`tests/Zerox.Api.Tests/Sample`](tests/Zerox.Api.Tests/Sample)
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

4. Controllers with the JSON as is, and the error middleware first:

       builder.Services.AddControllers().AddZeroxJson();
       app.UseZeroxErrorHandling();
       app.MapControllers();

5. One line per entity — the project adds its own `[Authorize]`:

       public sealed class CustomersController(IRepository<Customer> repository) : EntityController<Customer>(repository);

   It answers `POST api/Customers/query|groups|projection` (DevExtreme load options, server-side),
   `GET api/Customers/values/{column}`, and the CRUD. A service that writes more than one repository
   opens `IUnitOfWork` (`CustomerMerge`); many rows without loading them go through
   `IMassiveOpHandler<T>` (`OrdersController.CloseBefore`); a single-field action that must win uses
   `LastWriterWins` (`CustomersController.Rename`).

## Build and test

    dotnet build zerox.csharp.core.slnx -warnaserror
    dotnet test --solution zerox.csharp.core.slnx
    tools/verify.sh        # every check, [k/4]

Requires the .NET SDK 10.0.400 (see `global.json`).
