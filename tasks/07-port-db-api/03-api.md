# Zerox.Api — errors, JSON, the entity controller, and the example application

<block id="08-api-errors-json">
## TDDAB-8: Create Zerox.Api with the Error Middleware and the JSON As Is

<intro>
Creates `src/Zerox.Api` (references `src/Zerox.Domain`; `<FrameworkReference Include="Microsoft.AspNetCore.App" />`)
and `tests/Zerox.Api.Tests` (references `src/Zerox.Api`; packages `Microsoft.AspNetCore.TestHost` 10.0.0 —
added to `Directory.Packages.props` — and `Microsoft.EntityFrameworkCore.Sqlite`); add both to
`zerox.csharp.core.slnx`.

Delivers:
- `ApiError(ApiErrorCode Code, string Message)` (public sealed record) and `ApiErrorCode`
  {`InvalidRequest`, `Conflict`, `InternalError`} — enums serialize as their name;
- `ErrorHandlingMiddleware` (internal sealed) + `ApplicationBuilderExtensions.UseZeroxErrorHandling()`:
  - `DbUpdateConcurrencyException` where an entry has a concurrency token → **409** `ApiError(Conflict,
    "The row was changed by another user.")`, logged as Warning (an expected event, not a failure);
  - `DbUpdateConcurrencyException` on an entity WITHOUT token (EF throws it whenever an UPDATE/DELETE
    hits 0 rows, e.g. updating a missing row) → falls to the generic 500: the 409 belongs only to the
    opt-in lock;
  - `InvalidRequestException` → **400** `ApiError(InvalidRequest, message)`, not logged as error (the
    reason goes to whoever wrote the request);
  - anything else → **500** `ApiError(InternalError, "An unexpected error occurred.")`, logged as Error;
  - a successful request is not touched;
- `ZeroxJson.AddZeroxJson(this IMvcBuilder)`: `PropertyNamingPolicy = null` (names "as is") on MVC JSON
  options AND on `ConfigureHttpJsonOptions` (so OpenAPI uses the same); `ZeroxJson.AddConverters(JsonSerializerOptions)`:
  `JsonStringEnumConverter`, `DateOnlyJsonConverter`, `NullableDateOnlyJsonConverter` (`yyyy-MM-dd`) —
  one place, so they never diverge.

Test host for this block: `tests/Zerox.Api.Tests/TestHost.cs` — builds a `WebApplication` with
`builder.WebHost.UseTestServer()`, `Services.AddControllers().AddZeroxJson()` (so `ApiError.Code` is
written as its name), `UseZeroxErrorHandling()`, minimal endpoints that throw what each test
needs, and a Sqlite in-memory context with a locked entity (`Doc : IEntity, IHasOptimisticLock`) and a
plain one (`Plain : IEntity`) to produce REAL `DbUpdateConcurrencyException`s (a stale `Version` save; an
update of a missing `Plain` row). A capturing `ILoggerProvider` records levels.

Source: `<filo>/src/Filo.Web/Middleware/ErrorHandlingMiddleware.cs`, `<filo>/src/Filo.Core/Errors/AppError.cs`,
`<filo>/src/Filo.Web/FiloWebExtensions.cs` (`AddFiloWebJson`, `AggiungiConvertitori`, `UseFiloWebErrorHandling`),
`<filo>/src/Filo.Web/Configuration/DateOnlyJsonConverter.cs`. Tests:
`<filo>/tests/Filo.Server.Tests/Middleware/ErrorHandlingConcurrencyTests.cs`.

Files: `src/Zerox.Api/Zerox.Api.csproj`, `ApiError.cs`, `ErrorHandlingMiddleware.cs`,
`ApplicationBuilderExtensions.cs`, `ZeroxJson.cs`, `DateOnlyJsonConverter.cs`;
`tests/Zerox.Api.Tests/Zerox.Api.Tests.csproj`, `TestHost.cs`, `ErrorHandlingTests.cs`, `JsonTests.cs`.
</intro>

<red>
- test: Should_Answer409_WhenALockedRowWasChanged — body is `{"Code":"Conflict","Message":…}`
- test: Should_LogAWarning_WhenAConflictHappens — level Warning, not Error
- test: Should_Answer500_WhenAConcurrencyExceptionHasNoToken — update of a missing `Plain` row
- test: Should_Answer400WithTheReason_WhenAnInvalidRequestIsThrown — `Code` `InvalidRequest`, `Message` the exception message; nothing logged at Error
- test: Should_Answer500WithoutDetails_WhenAnythingElseIsThrown — the exception message does not appear in the body; logged at Error
- test: Should_NotTouchTheResponse_WhenTheRequestSucceeds — 200 and the endpoint's body
- test: Should_KeepPropertyNamesAsIs_WhenSerializing — `{"Id":1,"CreatedOn":…}`, never camelCase
- test: Should_WriteEnumsAsNames — `"Conflict"`, not `1`
- test: Should_WriteAndReadDateOnlyAsIsoDay — `"2026-10-27"` both ways, nullable included
- test: Should_ApplyTheSameOptions_ToHttpJsonOptions — `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>` has `PropertyNamingPolicy` null and the three converters
</red>

### Implementation

```csharp
namespace Zerox.Api;

public sealed record ApiError(ApiErrorCode Code, string Message);

public enum ApiErrorCode
{
    InvalidRequest,
    Conflict,
    InternalError,
}

internal sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException e) when (IsOptimisticLockConflict(e))
        {
            logger.LogWarning(e, "Optimistic lock conflict");
            await WriteAsync(context, HttpStatusCode.Conflict,
                new ApiError(ApiErrorCode.Conflict, "The row was changed by another user.")).ConfigureAwait(false);
        }
        catch (InvalidRequestException e)
        {
            await WriteAsync(context, HttpStatusCode.BadRequest,
                new ApiError(ApiErrorCode.InvalidRequest, e.Message)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception");
            await WriteAsync(context, HttpStatusCode.InternalServerError,
                new ApiError(ApiErrorCode.InternalError, "An unexpected error occurred.")).ConfigureAwait(false);
        }
    }

    // EF throws DbUpdateConcurrencyException on every UPDATE/DELETE that hits 0 rows, even without a token:
    // the 409 belongs only to entities with the opt-in lock.
    private static bool IsOptimisticLockConflict(DbUpdateConcurrencyException e) =>
        e.Entries.Any(entry => entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken));

    private static Task WriteAsync(HttpContext context, HttpStatusCode status, ApiError error)
    {
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsJsonAsync(error);   // uses the HTTP JSON options (AddZeroxJson)
    }
}
```

The middleware is the outermost catch of the application: catching `Exception` there is not "going on",
it is the place where an unexpected exception is turned into the 500 the client gets (and it is logged).
`LogWarning`/`LogError` with message templates, never `[LoggerMessage]` source generation (user rule:
code anyone reads). If the build reports CA1848, `.editorconfig` gets `dotnet_diagnostic.CA1848.severity = none`
for `src/Zerox.Api/**.cs` with the comment "plain logging calls, readable by anyone".

```csharp
namespace Zerox.Api;

public static class ZeroxJson
{
    public static IServiceCollection AddZeroxJson(this IMvcBuilder mvc)
    {
        ArgumentNullException.ThrowIfNull(mvc);
        mvc.AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = null; AddConverters(o.JsonSerializerOptions); });
        mvc.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.PropertyNamingPolicy = null; AddConverters(o.SerializerOptions); });
        return mvc.Services;
    }

    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new NullableDateOnlyJsonConverter());
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseZeroxErrorHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorHandlingMiddleware>();
}
```

<success>
- [ ] All RED tests of `ErrorHandlingTests` and `JsonTests` pass
- [ ] `src/Zerox.Api`, `tests/Zerox.Api.Tests` in the solution; versions only in `Directory.Packages.props`
- [ ] No Filo name or Italian in `src/Zerox.Api`
- [ ] `docs/catalog.md` has a `Zerox.Api` section; `README.md` lists `Zerox.Api`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="09-api-entity-controller">
## TDDAB-9: Add EntityController and the Example Application

<intro>
Delivers `EntityController<T>` (public abstract, `src/Zerox.Api/EntityController.cs`) and creates THE
EXAMPLE APPLICATION in `tests/Zerox.Api.Tests/Sample` (namespace `Zerox.Api.Tests.Sample`) — exactly what
a new project writes, and nothing more — with end-to-end HTTP tests of every controller action.

`EntityController<T>(IRepository<T> repository) : ControllerBase where T : class, IEntity`,
`[ApiController]`, `[Route("api/[controller]")]`, NO `[Authorize]` (the project adds its own), all actions
`virtual`:
- `POST query` ← `[FromBody] DataSourceLoadOptionsBase? options` (null → new) → `Search.RowsAsync(Repository.Query(), …)`
  — the DevExtreme DataLoadOptions: filtering, sorting, paging server-side; the body binds with any key case
  (`Skip`/`skip`), DevExtreme option names are not renamed;
- `POST groups` ← options → `Search.GroupsAsync(SearchSource(), …)`;
- `POST projection` ← options → `Search.ProjectionAsync(SearchSource(), …)`;
- `GET values/{column}` → `DistinctTextValues.IsTextColumn<T>(column)` false →
  `throw new InvalidRequestException($"'{column}' is not a text column of {typeof(T).Name}.")` (400 via the
  middleware, same shape as every other invalid request); else `DistinctTextValues.ValuesAsync(Repository.Query(), …)`;
- `GET {id}` → 404 or the entity; `POST` → add + save → 201 `CreatedAtAction(GetById)`;
  `PUT {id}` → id mismatch → 400 `ApiError(InvalidRequest, "The id in the route and in the body differ.")`,
  else `Update` + save → 200; `DELETE {id}` → 404 or delete + save → 204.
- `protected IRepository<T> Repository`; `protected virtual IQueryable<T> SearchSource() => Repository.Query();`
  (a controller with its own view of the rows overrides it).
- One repository per action: no unit of work in the base (whoever writes more than one repository opens
  the boundary — block 10).

The example (all public), laid out as a project's layers — each folder uses ONLY the folders before it and
the zerox library of its layer:
- `tests/Zerox.Api.Tests/Sample/Core/` (namespace `Zerox.Api.Tests.Sample.Core`): the APP's own contracts
  and interfaces only — empty in this block; block 10 puts `ICustomerMerge` there (zerox's contracts are in
  `Zerox.Core`, the app's in its Core);
- `tests/Zerox.Api.Tests/Sample/Db/` (`…Sample.Db`, uses `Zerox.Core.Data` and `Zerox.Db`): the entities
  `Customer : IEntity, IHasOptimisticLock` {`Id`, `Name`, `City`, `Version`, `List<Order> Orders`},
  `Order : IEntity` {`Id`, `CustomerId`, `Customer?`, `Date` `DateOnly`, `Amount` decimal,
  `Status` `OrderStatus {Open, Closed}`, `[JsonIgnore] string? InternalNote`}, and
  `SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options), IDbContext`, with
  `Customers`, `Orders` and `OnModelCreating` with `Amount` mapped `HasConversion<double>()` — with the
  comment "Sqlite cannot ORDER BY or SUM a decimal (EF Core: OrderByNotSupported,
  AggregateOperationNotSupported); a real database keeps decimal" — and ending with
  `modelBuilder.ApplyOptimisticLock()`;
  `Customer.Orders` carries `[JsonIgnore]` to break the JSON cycle (it is a navigation, so it is NOT a
  reserved field: `Orders` stays usable in `Select`);
- `tests/Zerox.Api.Tests/Sample/Domain/` (`…Sample.Domain`): empty in this block; block 10 puts the
  services there;
- `tests/Zerox.Api.Tests/Sample/Api/` (`…Sample.Api`, uses `Zerox.Api`):
  `CustomersController : EntityController<Customer>`, `OrdersController : EntityController<Order>`
  (constructor only in this block), and `SampleApp : IAsyncDisposable` — `static Task<SampleApp> StartAsync()`: one open Sqlite in-memory
  connection; `WebApplication.CreateBuilder()`, `WebHost.UseTestServer()`,
  `Services.AddZeroxDb<SampleDbContext>(o => o.UseSqlite(connection))`,
  `Services.AddControllers().AddApplicationPart(typeof(SampleApp).Assembly).AddZeroxJson()`,
  `UseZeroxErrorHandling()`, `MapControllers()`, `EnsureCreated`, `StartAsync`; exposes `HttpClient Client`,
  `SampleDbContext NewContext()` (to seed and to check the database), `SeedAsync()` (3 customers, 25 orders
  with known amounts, dates, statuses).

Source: `<filo>/src/Filo.Server/Controllers/BaseController.cs` (without `[Authorize]`, without MCP
attributes, without task references). Tests: `<filo>/tests/Filo.Server.Tests/Controllers/BaseControllerQueryTests.cs`,
`BaseControllerValoriDistintiTests.cs`.

Files: `src/Zerox.Api/EntityController.cs`; `tests/Zerox.Api.Tests/Sample/Db/Customer.cs`, `Order.cs`,
`SampleDbContext.cs`; `Sample/Api/CustomersController.cs`, `OrdersController.cs`, `SampleApp.cs`;
`tests/Zerox.Api.Tests/Sample/EntityControllerApiTests.cs`.
</intro>

<red>
- test: Should_PageTheRows_WhenQueryHasSkipAndTake — `POST api/Orders/query {"Skip":10,"Take":5,"RequireTotalCount":true}` → 5 rows, `totalCount` 25
- test: Should_FilterOnTheServer_WhenQueryHasAFilter — `["Status","=","Closed"]` returns only closed orders; `["Customer.Name","contains","a"]` filters through the navigation
- test: Should_SortOnTheServer_WhenQueryHasSort — `Amount` desc
- test: Should_ReturnEverything_WhenTheQueryBodyIsNull
- test: Should_AcceptCamelCaseOptionKeys — `{"skip":0,"take":2}` works like `{"Skip":0,"Take":2}`
- test: Should_ReturnTheTotalSummary_WhenAsked — `TotalSummary [{"Selector":"Amount","SummaryType":"sum"}]` equals the seeded sum
- test: Should_Answer400WithTheReason_WhenTheFilterIsWrong — unknown field `Nope`: 400, `Code` `InvalidRequest`, message names `Nope`
- test: Should_Answer400_WhenAReservedFieldIsNamed — `InternalNote` in a filter
- test: Should_ReturnGroups_WhenGroupsIsCalled — `POST api/Orders/groups` by `Status` with sum of `Amount`: keys and sums, no rows
- test: Should_ReturnNamesInsteadOfIds_WhenProjectionIsCalled — `POST api/Orders/projection {"Select":["Amount","Customer.Name"]}` → objects with keys `Amount` and `Customer.Name`
- test: Should_ReturnDistinctCities_WhenValuesIsCalled — `GET api/Customers/values/City`: distinct, ordered, no empty
- test: Should_Answer400_WhenValuesNamesAColumnThatIsNotText — `values/Version`, `values/Nope`
- test: Should_Create_WhenPosted — 201, `Location` points to the new id, `GET` returns it with property names as is
- test: Should_Answer404_WhenGettingAMissingId
- test: Should_Update_WhenPut — 200 and a new context sees the change; `Version` became 1
- test: Should_Answer400_WhenPutIdsDiffer
- test: Should_Delete_WhenDeleted — 204, then 404
- test: Should_Answer409_WhenPutCarriesAStaleVersion — two clients read the same customer; the second PUT with `Version` 0 after the first → 409 `Conflict`
</red>

### Implementation

```csharp
namespace Zerox.Api;

[ApiController]
[Route("api/[controller]")]
public abstract class EntityController<T>(IRepository<T> repository) : ControllerBase where T : class, IEntity
{
    protected IRepository<T> Repository { get; } = repository;

    // POST because the options travel in the body; the meaning is a read.
    [HttpPost("query")]
    public virtual async Task<ActionResult<DataSourceResult<T>>> Query(
        [FromBody] DataSourceLoadOptionsBase? options, CancellationToken ct) =>
        Ok(await Search.RowsAsync(Repository.Query(), options ?? new DataSourceLoadOptionsBase(), ct).ConfigureAwait(false));

    [HttpPost("groups")]
    public virtual async Task<ActionResult<GroupsResult>> Groups([FromBody] DataSourceLoadOptionsBase options, CancellationToken ct) =>
        Ok(await Search.GroupsAsync(SearchSource(), options, ct).ConfigureAwait(false));

    [HttpPost("projection")]
    public virtual async Task<ActionResult<DataSourceResult<IDictionary<string, object?>>>> Projection(
        [FromBody] DataSourceLoadOptionsBase options, CancellationToken ct) =>
        Ok(await Search.ProjectionAsync(SearchSource(), options, ct).ConfigureAwait(false));

    // The rows groups and projection work on. A controller with its own view overrides it.
    protected virtual IQueryable<T> SearchSource() => Repository.Query();

    [HttpGet("values/{column}")]
    public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, CancellationToken ct)
    {
        // The column name comes from the URL: checked against the real properties before any query.
        if (!DistinctTextValues.IsTextColumn<T>(column))
            throw new InvalidRequestException($"'{column}' is not a text column of {typeof(T).Name}.");
        return Ok(await DistinctTextValues.ValuesAsync(Repository.Query(), column, ct).ConfigureAwait(false));
    }

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<T>> GetById(int id, CancellationToken ct) =>
        await Repository.GetByIdAsync(id, ct).ConfigureAwait(false) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        await Repository.AddAsync(entity, ct).ConfigureAwait(false);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        if (id != entity.Id)
            return BadRequest(new ApiError(ApiErrorCode.InvalidRequest, "The id in the route and in the body differ."));
        Repository.Update(entity);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await Repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null) return NotFound();
        Repository.Delete(entity);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return NoContent();
    }
}
```

`Update` of a detached `Customer` carrying `Version` = 0 while the row is at 1: `Update` marks every
property modified with the original `Version` = the value in the body, so the UPDATE's WHERE uses the
client's version and EF throws → the middleware answers 409. That is the opt-in lock working through the
generic controller with no extra code.

```csharp
namespace Zerox.Api.Tests.Sample.Api;   // SampleDbContext lives in …Sample.Db

public sealed class CustomersController(IRepository<Customer> repository) : EntityController<Customer>(repository);

public sealed class OrdersController(IRepository<Order> repository) : EntityController<Order>(repository);

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sqlite cannot ORDER BY or SUM a decimal; a real database keeps decimal.
        modelBuilder.Entity<Order>().Property(o => o.Amount).HasConversion<double>();
        modelBuilder.ApplyOptimisticLock();
    }
}
```

<success>
- [ ] All RED tests of `EntityControllerApiTests` pass over HTTP on the sample application; blocks 01–08 tests still pass
- [ ] The sample contains no line of infrastructure beyond: entities, `SampleDbContext`, two one-line controllers, `SampleApp` (the registration calls)
- [ ] `EntityController<T>` in `docs/catalog.md`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="10-sample-end-to-end">
## TDDAB-10: Complete the Example — Unit of Work, Massive Operation and Last Writer Wins End to End

<intro>
Completes the example application of block 09 (`tests/Zerox.Api.Tests/Sample`) with the three things a
project builds ON the foundation, each tested at every level — repository (database), service, and
end-to-end over HTTP — and documents the example in `README.md`.

1. **Unit of work** — the app's contract `ICustomerMerge` in `Sample/Core`
   (`Task<bool> MergeAsync(int fromId, int intoId, CancellationToken ct = default)`) and its implementation
   `CustomerMerge(IRepository<Customer> customers, IRepository<Order> orders) : ICustomerMerge` in
   `Sample/Domain` (public sealed; registered `AddScoped<ICustomerMerge, CustomerMerge>()` in `SampleApp`;
   the controller depends on `ICustomerMerge`): `Task<bool> MergeAsync(int fromId, int intoId, CancellationToken)`
   moves every order of `from` to `into` and deletes `from`, inside ONE boundary
   (`customers.OpenUnitOfWork()`): each repository saves as if alone (swallowed), `ApplyAsync` writes once.
   Missing customer → false, nothing written. `fromId == intoId` → `ArgumentException`.
   Endpoint: `POST api/Customers/{id}/merge-into/{intoId}` on `CustomersController` → 204, or 404.
2. **Massive operation** — `OrdersController`: `POST api/Orders/close-before/{date}` (date `yyyy-MM-dd`)
   → `OpenMassiveAsync`, `UpdateWhereAsync(o => o.Status == Open && o.Date < date, f => f.Set(o => o.Status, Closed))`,
   `CommitAsync` → 200 with `{"Closed": n}`.
3. **Last writer wins** — `CustomersController`: `PUT api/Customers/{id}/name` with body
   `{"Name": "...", "Version": n}` → `LastWriterWins.SaveAsync<Customer>(id, _ => true, (c, _) => { c.Name = body.Name; return Task.CompletedTask; }, body.Version, ct)`
   → 200 with the customer, or 404. `LastWriterWins` registered scoped in `SampleApp`. The body type is a
   `public sealed record RenameRequest(string Name, int Version)` in the sample: a request shape for one
   action, not an entity.

`CustomersController` and `OrdersController` gain the constructor parameters they need
(`ICustomerMerge`, `LastWriterWins`); `SampleApp` registers them. Nothing is added to `src/`: every
capability comes from blocks 01–09 (`IUnitOfWork`, `IMassiveOpHandler<T>`, `LastWriterWins`,
`EntityController<T>`, the middleware).

`README.md` gains a section **"Building on the database foundation"**: the five steps (entities,
`MyContext : DbContext, IDbContext` + `ApplyOptimisticLock`, `AddZeroxDb`, `AddControllers().AddZeroxJson()`
+ `UseZeroxErrorHandling`, one-line controllers) with the code taken from the sample, and a pointer to
`tests/Zerox.Api.Tests/Sample` as the complete example.

Files: `tests/Zerox.Api.Tests/Sample/Core/ICustomerMerge.cs`, `Sample/Domain/CustomerMerge.cs`; `Sample/Api/RenameRequest.cs`,
`CustomersController.cs`, `OrdersController.cs`, `SampleApp.cs`; tests `tests/Zerox.Api.Tests/Sample/SampleRepositoryTests.cs`,
`CustomerMergeTests.cs`, `SampleApiTests.cs`; `README.md`, `docs/catalog.md` (a line "complete example →
tests/Zerox.Api.Tests/Sample").
</intro>

<red>
- test: Should_ReadTheSeededRows_ThroughTheRepository — database level: `IRepository<Order>` from the sample's provider; `Query().Where(o => o.Customer!.Name == …)` counts the seeded orders
- test: Should_MoveOrdersAndDeleteTheCustomer_WhenMerged — service level: after `MergeAsync` a new context sees every order on `into` and no `from`
- test: Should_WriteOnce_WhenMerged — service level: during `MergeAsync` exactly ONE `SavedChanges` event of the context has `EntitiesSavedCount > 0` (swallowed saves raise it too, with 0: EF Core 10 `DbContext.SaveChangesAsync` invokes `SavedChanges` after the interception result)
- test: Should_WriteNothing_WhenTheMergeFailsHalfway — service level: the test's context carries a `DbCommandInterceptor` that throws when a command starts with `DELETE` (commands run only in the applying write, never in swallowed saves; the `UPDATE`s of the orders run first, then the `DELETE` fails and EF rolls back its own transaction); `MergeAsync` throws, a new context sees every order on its old customer and both customers present, and the tracker is empty
- test: Should_ReturnFalseAndWriteNothing_WhenACustomerIsMissing
- test: Should_Throw_WhenMergingACustomerIntoItself
- test: Should_Answer204AndMerge_WhenMergeIsCalledOverHttp — end to end
- test: Should_Answer404_WhenMergeNamesAMissingCustomer — end to end
- test: Should_CloseOnlyOpenOrdersBeforeTheDate_WhenCloseBeforeIsCalled — end to end: `{"Closed": n}` equals the seeded count; a new context sees exactly those closed; orders after the date untouched
- test: Should_RollBack_WhenTheMassiveOperationIsNotCommitted — service level on the repository: `OpenMassiveAsync` + update + dispose without commit leaves the orders open
- test: Should_RenameWithTheLastWriterWinning_WhenTheClientVersionIsStale — end to end: client A renames (Version 0 → 1); client B renames with `Version` 0 → 200, the name is B's, `Version` 2
- test: Should_Answer404_WhenRenamingAMissingCustomer — end to end
- test: Should_StillAnswer409_WhenAFullPutIsStale — end to end: the full-entity PUT keeps the conflict (409) while the single-field rename wins — the two semantics side by side
- test: Should_ShowTheWholeLifecycle — end to end in one test: create two customers and orders over HTTP, query with filter and paging, project names, group sums, merge, close before a date, rename, delete — every step's status and the database at the end
</red>

### Implementation

```csharp
namespace Zerox.Api.Tests.Sample.Domain;

// The example of a unit of work: two repositories, one write.
public sealed class CustomerMerge(IRepository<Customer> customers, IRepository<Order> orders) : ICustomerMerge
{
    public async Task<bool> MergeAsync(int fromId, int intoId, CancellationToken ct = default)
    {
        if (fromId == intoId) throw new ArgumentException("A customer cannot be merged into itself.", nameof(intoId));

        await using var work = customers.OpenUnitOfWork();
        var from = await customers.GetByIdAsync(fromId, ct);
        var into = await customers.GetByIdAsync(intoId, ct);
        if (from is null || into is null) return false;   // disposed without applying: nothing written

        foreach (var order in await orders.Query(tracked: true).Where(o => o.CustomerId == fromId).ToListAsync(ct))
            order.Customer = into;                         // EF sets the key from the navigation
        await orders.SaveChangesAsync(ct);                 // swallowed

        customers.Delete(from);
        await customers.SaveChangesAsync(ct);              // swallowed

        await work.ApplyAsync(ct);                         // ONE write
        return true;
    }
}
```

```csharp
namespace Zerox.Api.Tests.Sample.Api;

public sealed class CustomersController(
    IRepository<Customer> repository, ICustomerMerge merge, LastWriterWins lastWriterWins)
    : EntityController<Customer>(repository)
{
    [HttpPost("{id:int}/merge-into/{intoId:int}")]
    public async Task<IActionResult> MergeInto(int id, int intoId, CancellationToken ct) =>
        await merge.MergeAsync(id, intoId, ct) ? NoContent() : NotFound();

    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct) =>
        await lastWriterWins.SaveAsync<Customer>(id, _ => true,
            (customer, _) => { customer.Name = body.Name; return Task.CompletedTask; }, body.Version, ct)
            is { } saved ? Ok(saved) : NotFound();
}

public sealed class OrdersController(IRepository<Order> repository) : EntityController<Order>(repository)
{
    [HttpPost("close-before/{date}")]
    public async Task<ActionResult<object>> CloseBefore(DateOnly date, CancellationToken ct)
    {
        await using var massive = await Repository.OpenMassiveAsync(ct);
        var closed = await massive.UpdateWhereAsync(
            o => o.Status == OrderStatus.Open && o.Date < date,
            fields => fields.Set(o => o.Status, OrderStatus.Closed), ct);
        await massive.CommitAsync(ct);
        return Ok(new { Closed = closed });
    }
}
```

Test code awaits without `ConfigureAwait` (CA2007 is off in tests).

<success>
- [ ] All RED tests of `SampleRepositoryTests`, `CustomerMergeTests`, `SampleApiTests` pass; every earlier test still passes
- [ ] Nothing new under `src/` in this block
- [ ] `README.md` has "Building on the database foundation" with code matching the sample
- [ ] `tools/verify.sh` passes all four steps (end of plan: unfiltered)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
