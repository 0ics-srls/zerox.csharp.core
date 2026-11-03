<block id="05-sample-services">
## TDDAB-5: Give the Sample its own services and answer their outcomes over HTTP

<intro>
The example application (`tests/Zerox.Ef/Api/Sample`, test project `Zerox.Ef.Api.Tests`) gets the services a
project writes, and `EntityController<T>` gets the one translation of an outcome into HTTP.
- `src/Zerox.Ef/Api/EntityController.cs` gains `protected ActionResult Answer<TValue>(OpResult<TValue> result)`:
  one switch over `OpStatus` — Ok → `Ok(value)`, Created → `StatusCode(201, value)`, Deleted → `NoContent()`,
  NotFound → `NotFound()`, Conflict → `Problem(detail: reason, statusCode: 409)`, Invalid and Failed →
  `Problem(detail: reason, statusCode: 400)`, Faulted → the captured exception rethrown with
  `ExceptionDispatchInfo.Throw` (the stack kept) so `ErrorHandlingMiddleware` logs it and answers 500 exactly as
  for any other exception; `_ => throw new UnreachableException()`. `Problem(…)` goes through the platform's
  ProblemDetails factory, so `AddZeroxProblemDetails` adds the `Code` of the status (a Failed answer reads
  `InvalidRequest`). The base actions are untouched in this block (block 06 moves them).
- Sample contracts in `Sample/Core/`: `ICustomerService : IEntityService<Customer>` with
  `Task<OpResult<Customer>> MergeIntoAsync(int id, int intoId, CancellationToken ct = default)` and
  `Task<OpResult<Customer>> RenameAsync(int id, string name, uint version, CancellationToken ct = default)`;
  `IOrderService : IEntityService<Order>` with `Task<OpResult<int>> CloseBeforeAsync(DateOnly date, CancellationToken ct = default)`.
  `Sample/Core/ICustomerMerge.cs` and `Sample/Domain/CustomerMerge.cs` are deleted: their unit of work moves,
  unchanged, into `CustomerService.MergeIntoAsync`.
- Sample services in `Sample/Domain/`: `CustomerService(IRepository<Customer> customers, IRepository<Order> orders)
  : EntityService<Customer>(customers), ICustomerService` and `OrderService(IRepository<Order> orders) :
  EntityService<Order>(orders), IOrderService` — every operation through `RunAsync`; the rename and the close are
  today's controller code (`CustomersController.Rename`, `OrdersController.CloseBefore`) moved; the rename uses the
  base's `SaveUnlessMissingAsync`.
- `SampleApp.StartAsync`: `AddZeroxDomain().AddEntityService<Customer, ICustomerService, CustomerService>()
  .AddEntityService<Order, IOrderService, OrderService>()` in place of `AddScoped<ICustomerMerge, CustomerMerge>()`.
- Sample controllers: `CustomersController(IRepository<Customer> repository, ICustomerService customers)` —
  `MergeInto` → `Answer(await customers.MergeIntoAsync(id, intoId, ct))` (declares 204, 400, 404), `Rename` →
  `Answer(await customers.RenameAsync(id, body.Name, body.Version, ct))`; `OrdersController(IRepository<Order>
  repository, IOrderService orders)` — `CloseBefore` answers `Ok(new { Closed = n })` on success, `Answer(result)`
  otherwise. They still pass the repository to the base (block 06 changes the base).
- `tests/Zerox.Ef/Api/Sample/openapi/Sample.json` changes (the merge declares 400): the document test rewrites
  the file and fails once; the second run passes. The TypeScript client is not touched.
Uses blocks 01, 03, 04. Tests: `tests/Zerox.Ef/Api/AnswerTests.cs` (in process: a
`ProbeController : EntityController<Plain>` exposing `Answer`, its `ControllerContext.HttpContext.RequestServices` a
provider built with `AddControllers().AddZeroxJson()` and `AddZeroxProblemDetails()`, the repository argument from a
`TestHost`'s scope), `tests/Zerox.Ef/Api/Sample/CustomerServiceTests.cs` (replaces `CustomerMergeTests.cs`, whose private nested
`FailingDelete : DbCommandInterceptor` moves into it unchanged),
`tests/Zerox.Ef/Api/Sample/OrderServiceTests.cs`, one test added to `SampleApiTests.cs`.
Focused run: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-class "*AnswerTests" --filter-class "*CustomerServiceTests" --filter-class "*OrderServiceTests" --filter-class "*SampleApiTests"`.
</intro>

<red>
- test: Should_MapEveryStatus — every `Enum.GetValues<OpStatus>()` except Faulted goes through `Answer` without `UnreachableException` (a new status without its answer turns this red)
- test: Should_Answer200WithTheValue_WhenOk — `OkObjectResult`, Value the given value
- test: Should_Answer201WithTheValue_WhenCreated — `ObjectResult` StatusCode 201, Value the given value
- test: Should_Answer204_WhenDeleted — `NoContentResult`; Should_Answer404_WhenNotFound — `NotFoundResult`
- test: Should_Answer409WithTheReasonAndCode_WhenConflict — `ObjectResult` 409 whose `ProblemDetails` has Detail the reason and `Extensions["Code"]` "Conflict"
- test: Should_Answer400WithTheReasonAndCode_WhenInvalidOrFailed — Theory Invalid, Failed: 400, Detail the reason, Code "InvalidRequest"
- test: Should_RethrowTheCapturedException_WhenFaulted — `Answer(OpResult.Faulted<int>(e))` throws the same instance `e`, its stack trace still naming the method that threw it first
- test: Should_MoveOrdersAndDeleteTheCustomer_WhenMerged (CustomerServiceTests) — `MergeIntoAsync(Bolt, Acme)`: Deleted; Bolt gone; Acme has 17 orders
- test: Should_WriteOnce_WhenMerged — one `SavedChanges` with entities
- test: Should_WriteNothingAndReturnFaulted_WhenTheMergeFailsHalfway — the provider of the old test (`AddZeroxDb` with the `FailingDelete` interceptor) plus `AddZeroxDomain().AddEntityService<Customer, ICustomerService, CustomerService>()`: Faulted (the interceptor's exception inside), orders and customers unchanged, nothing tracked
- test: Should_ReturnNotFoundAndWriteNothing_WhenACustomerIsMissing — `(Bolt, 404)` and `(404, Acme)`: NotFound, orders unchanged
- test: Should_ReturnInvalid_WhenMergingACustomerIntoItself — `(Acme, Acme)`: Invalid, Reason "A customer cannot be merged into itself."
- test: Should_RenameOnlyTheName_WhenTheVersionIsTheOneRead — `RenameAsync(Acme, "Acme2", versionRead)`: Ok, Value.Name "Acme2", City unchanged in a new context
- test: Should_ReturnNotFound_WhenRenamingAMissingCustomer — NotFound
- test: Should_ReturnConflict_WhenRenamingAtAStaleVersion (PostgreSQL) — the row changed by another context after the read: Conflict
- test: Should_CloseOnlyOpenOrdersBeforeTheDate (OrderServiceTests) — `CloseBeforeAsync(2026-01-11)`: Ok with the number of open orders dated before it in the seed, read back as Closed, later ones still Open
- test: Should_Answer400_WhenMergingACustomerIntoItselfOverHttp (SampleApiTests) — `POST api/Customers/1/merge-into/1`: 400 with Code "InvalidRequest" and the reason
- test: the existing `SampleApiTests` (merge 204/404, close-before, rename, rename 409/404, the lifecycle) pass unchanged
</red>

### Implementation

```csharp
// src/Zerox.Ef/Api/EntityController.cs — added (the rest unchanged in this block)
/// <summary>
/// The one translation of an outcome into HTTP: Ok 200, Created 201, Deleted 204, NotFound 404, Conflict 409,
/// Invalid and Failed 400 with the reason; Faulted rethrows its exception to the error middleware (logged, 500).
/// </summary>
protected ActionResult Answer<TValue>(OpResult<TValue> result)
{
    ArgumentNullException.ThrowIfNull(result);
    return result.Status switch
    {
        OpStatus.Ok => Ok(result.Value),
        OpStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
        OpStatus.Deleted => NoContent(),
        OpStatus.NotFound => NotFound(),
        OpStatus.Conflict => Problem(detail: result.Reason, statusCode: StatusCodes.Status409Conflict),
        OpStatus.Invalid or OpStatus.Failed => Problem(detail: result.Reason, statusCode: StatusCodes.Status400BadRequest),
        OpStatus.Faulted => Rethrow(result.Exception!),
        _ => throw new UnreachableException($"OpStatus {result.Status} has no answer."),
    };
}

// The middleware is the one place that logs an unexpected exception and answers 500: a Faulted outcome goes there.
private static ActionResult Rethrow(Exception exception)
{
    ExceptionDispatchInfo.Throw(exception);
    throw new UnreachableException();
}
```

```csharp
// tests/Zerox.Ef/Api/Sample/Domain/CustomerService.cs
public sealed class CustomerService(IRepository<Customer> customers, IRepository<Order> orders)
    : EntityService<Customer>(customers), ICustomerService
{
    // The unit of work of the example: two repositories, one write.
    public Task<OpResult<Customer>> MergeIntoAsync(int id, int intoId, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            if (id == intoId)
                return OpResult.Invalid<Customer>("A customer cannot be merged into itself.");
            await using var work = Repository.OpenUnitOfWork();
            var from = await Repository.GetByIdAsync(id, c);
            var into = await Repository.GetByIdAsync(intoId, c);
            if (from is null || into is null)
                return OpResult.NotFound<Customer>();   // disposed without applying: nothing written
            foreach (var order in await orders.Query(tracked: true).Where(o => o.CustomerId == id).ToListAsync(c))
                order.Customer = into;
            await orders.SaveChangesAsync(c);           // swallowed: the unit of work writes
            Repository.Delete(from);
            await Repository.SaveChangesAsync(c);       // swallowed
            await work.ApplyAsync(c);                   // ONE write
            return OpResult.Deleted<Customer>();
        }, ct);

    // One field at the version the client read, without reading the row.
    public Task<OpResult<Customer>> RenameAsync(int id, string name, uint version, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            Repository.Update(new Customer { Id = id, Name = name, Version = version }, [nameof(Customer.Name)]);
            if (!await SaveUnlessMissingAsync(id, c))
                return OpResult.NotFound<Customer>();
            return OpResult.Ok(await Repository.Query().SingleAsync(x => x.Id == id, c));
        }, ct);
}
```

```csharp
// tests/Zerox.Ef/Api/Sample/Domain/OrderService.cs
public sealed class OrderService(IRepository<Order> orders) : EntityService<Order>(orders), IOrderService
{
    // A massive operation: many rows updated without loading them.
    public Task<OpResult<int>> CloseBeforeAsync(DateOnly date, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            await using var massive = await Repository.OpenMassiveAsync(c);
            var closed = await massive.UpdateWhereAsync(
                o => o.Status == OrderStatus.Open && o.Date < date,
                fields => fields.Set(o => o.Status, OrderStatus.Closed),
                c);
            await massive.CommitAsync(c);
            return OpResult.Ok(closed);
        }, ct);
}
```

```csharp
// tests/Zerox.Ef/Api/Sample/Api/CustomersController.cs (this block)
public sealed class CustomersController(IRepository<Customer> repository, ICustomerService customers)
    : EntityController<Customer>(repository)
{
    [HttpPost("{id:int}/merge-into/{intoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeInto(int id, int intoId, CancellationToken ct) =>
        Answer(await customers.MergeIntoAsync(id, intoId, ct));

    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Answer(await customers.RenameAsync(id, body.Name, body.Version, ct));
    }
}
```

`docs/catalog.md`: the `EntityController<T>` row names `Answer`; the Sample row names `CustomerService`,
`OrderService` in place of `CustomerMerge`.

<success>
- [ ] `Answer` maps every status; a Faulted outcome reaches the middleware with its original stack
- [ ] The Sample's merge, rename and close run in `CustomerService` / `OrderService`; `ICustomerMerge` and `CustomerMerge` are gone
- [ ] `openapi/Sample.json` equals the served document
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` → all pass; `tools/test-postgres.sh --filter-class "*CustomerServiceTests" --filter-class "*SampleApiTests"` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="06-api-over-service">
## TDDAB-6: Put EntityController<T> on the entity service — the transport never touches the storage

<intro>
`src/Zerox.Ef/Api/EntityController.cs` becomes `EntityController<T>(IEntityService<T> service)`: a protected
`Service` property; **no** `Repository`, `MaxTake`, `SearchSource` or `SaveUnlessMissingAsync` any more (they live
in `EntityService<T>`, block 03). Routes, verbs, parameter binding and answers stay exactly as today:
- `Load([FromBody] LoadOptions? options)` → `Answer(await Service.LoadAsync(options ?? new LoadOptions(), ct))`,
  declared `Task<ActionResult<Loaded>>`;
- `Values(column)` → `Answer(await Service.ValuesAsync(column, ct))`;
- `GetById(id)` → `Answer(await Service.GetAsync(id, ct))`;
- `Create(entity)` → a Created outcome answers `CreatedAtAction(nameof(GetById), new { id = value.Id }, value)`,
  any other `Answer(result)`;
- `Update(id, entity)` → the route/body id check stays the transport's (`InvalidRequestException` → 400 by the
  middleware), then `Answer(await Service.UpdateAsync(entity, ct))`;
- `Delete(id, [FromQuery] ulong? version)` → `Answer(await Service.DeleteAsync(id, version, ct))`.
`ErrorHandlingMiddleware` calls `OptimisticLockConflict.Is` (block 03) and loses its private
`IsOptimisticLockConflict`. `ZeroxOpenApi`: `LoadResults` keeps documenting the load as `LoadResultOf<T>`;
`UnusedLoadResult` removes the schema `Loaded` (`nameof(Loaded)`, the declared type now) instead of DevExtreme's
`LoadResult`; the request body is documented as `LoadOptions` (with `SortingInfo`, `GroupingInfo`, `SummaryInfo`,
the same names as before). The Sample: `CustomersController(ICustomerService customers) :
EntityController<Customer>(customers)`, `OrdersController(IOrderService orders) : EntityController<Order>(orders)`;
`SampleApp` already registers the services (block 05). The TypeScript client of the Sample
(`tests/Zerox.Ef/Api/Sample/client/src/load.ts`) imports `LoadOptions` instead of `DataSourceLoadOptionsBase`
(both places: the `LoadCall` type and `body`). `openapi/Sample.json` is rewritten by the document test (fails
once, passes on the second run). `README.md` "Building on the database foundation" shows a controller over its
service and a derived service; `docs/catalog.md` updates the `EntityController<T>` row (over `IEntityService<T>`,
`Answer`) and the error-middleware row.
`tests/Zerox.Ef/Api/AnswerTests.cs` (block 05) builds its `ProbeController` with an `EntityService<Plain>` over the
`TestHost` scope's `IRepository<Plain>` instead of the repository.
Uses blocks 01-05. Tests: `tests/Zerox.Ef/Api/ControllerSurfaceTests.cs`; the existing
`EntityControllerApiTests`, `ErrorHandlingTests`, `ProblemDetailsTests`, `OpenApiDocumentTests`, `SampleApiTests`
are the behaviour that must not change.
Focused run: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-class "*ControllerSurfaceTests" --filter-class "*EntityControllerApiTests" --filter-class "*OpenApiDocumentTests"`.
</intro>

<red>
- test: Should_TakeTheServiceNotTheRepository_WhenTheControllerIsBuilt (ControllerSurfaceTests) — the only constructor of `EntityController<>` has one parameter, of type `IEntityService<T>`
- test: Should_ExposeNoStorage_WhenTheControllerIsInspected — `EntityController<>` has no member named `Repository`, `SaveUnlessMissingAsync`, `SearchSource` or `MaxTake` (any binding flags)
- test: Should_BindTheLoadOptions_AsTheServiceType — the `Load` action's body parameter is of type `LoadOptions` and its declared result `ActionResult<Loaded>`
- test: Should_DocumentTheLoadOptions_WhenTheDocumentIsServed (OpenApiDocumentTests) — the load's request body references the schema `LoadOptions`; the document has no schema `DataSourceLoadOptionsBase`, `Loaded` or `LoadResult`; `LoadResultOfCustomer` and `LoadGroup` are still there
- test: Should_Answer409_WhenACustomServiceConflicts — over HTTP, a stale rename answers 409 with Code "Conflict" (existing `Should_Answer409_WhenARenameCarriesAStaleVersion`, PostgreSQL, unchanged)
- test: every existing test of `EntityControllerApiTests` (load page/filter/sort/null body/without page/count/camelCase keys/summary/wrong filter/reserved field/groups/select, values, create, get 404, put/version/reserved/404/400, delete/404/400/409) passes unchanged
- test: every existing test of `ErrorHandlingTests` (409 with and without a token, 400, 500 without details, started response, client gone) passes unchanged
- test: `ProblemDetailsTests.Should_AnswerAProblemDetailsWithTheCode_ForEveryFailure` passes unchanged (the Code of every failure is the same as before)
</red>

### Implementation

```csharp
// src/Zerox.Ef/Api/EntityController.cs
/// <summary>
/// The controller of an entity: HTTP over its <see cref="IEntityService{T}"/> — one load (rows, columns, groups),
/// distinct values, the CRUD at the version the client read. It never touches the storage: every action asks the
/// service and answers its outcome (<see cref="Answer{TValue}"/>). A project writes
/// `class XController(IEntityService&lt;X&gt; s) : EntityController&lt;X&gt;(s)` — or receives its derived service —
/// and puts its own [Authorize]: the base has none.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class EntityController<T>(IEntityService<T> service) : ControllerBase where T : class, IEntity
{
    /// <summary>The service of the entity, for the actions a derived controller adds.</summary>
    protected IEntityService<T> Service { get; } = service;

    [HttpPost("load")]
    public virtual async Task<ActionResult<Loaded>> Load([FromBody] LoadOptions? options, CancellationToken ct) =>
        Answer(await Service.LoadAsync(options ?? new LoadOptions(), ct).ConfigureAwait(false));

    [HttpGet("values/{column}")]
    public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, CancellationToken ct) =>
        Answer(await Service.ValuesAsync(column, ct).ConfigureAwait(false));

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<T>> GetById(int id, CancellationToken ct) =>
        Answer(await Service.GetAsync(id, ct).ConfigureAwait(false));

    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var result = await Service.CreateAsync(entity, ct).ConfigureAwait(false);
        return result is { Status: OpStatus.Created, Value: { } created }
            ? CreatedAtAction(nameof(GetById), new { id = created.Id }, created)
            : Answer(result);
    }

    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (id != entity.Id)
            throw new InvalidRequestException("The id in the route and in the body differ.");
        return Answer(await Service.UpdateAsync(entity, ct).ConfigureAwait(false));
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, [FromQuery] ulong? version, CancellationToken ct) =>
        Answer(await Service.DeleteAsync(id, version, ct).ConfigureAwait(false));

    // Answer and Rethrow as written by block 05.
}
```
(each action keeps its `///` of today, with "the service" in place of "the repository")

```csharp
// src/Zerox.Ef/Api/ErrorHandlingMiddleware.cs — the one change
catch (DbUpdateConcurrencyException e) when (OptimisticLockConflict.Is(e))
```

```csharp
// src/Zerox.Ef/Api/ZeroxOpenApi.cs — UnusedLoadResult
.Any(m => m.Schema is OpenApiSchemaReference { Reference.Id: nameof(Loaded) }) ?? false;
if (!used)
    document.Components?.Schemas?.Remove(nameof(Loaded));
```

```ts
// tests/Zerox.Ef/Api/Sample/client/src/load.ts — the generated request type's new name
import type { LoadOptions, /* … the other imports unchanged */ } from "./api/model";
export type LoadCall = (options?: LoadOptions | null) => Promise<{ /* unchanged */ }>;
const body = <T>(request: LoadRequest<T>): LoadOptions => { /* unchanged */ };
```

`README.md` ("Building on the database foundation") shows:
```csharp
builder.Services.AddZeroxDb<AppDbContext>(o => o.UseNpgsql(connection))
    .AddZeroxDomain()
    .AddEntityService<Customer, ICustomerService, CustomerService>();   // a derived service, when there is one

public sealed class OrdersController(IEntityService<Order> service) : EntityController<Order>(service);
public sealed class CustomersController(ICustomerService customers) : EntityController<Customer>(customers) { /* its actions call customers and Answer(...) */ }
```
and a **Breaking** paragraph for a consumer: `EntityController<T>` takes `IEntityService<T>` (call
`AddZeroxDomain()`), a derived action calls its service and `Answer(...)` instead of `Repository` /
`SaveUnlessMissingAsync`; the load body type is `LoadOptions` (a generated client renames
`DataSourceLoadOptionsBase`).

<success>
- [ ] `EntityController<T>` receives `IEntityService<T>` and answers every action through `Answer`; no storage member is left on it
- [ ] Every existing API, error-handling, problem-details and Sample test passes unchanged; `openapi/Sample.json` equals the served document
- [ ] The Sample's TypeScript client passes: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-class "*ClientTests"` → pass
- [ ] `README.md` and `docs/catalog.md` describe the controller over the service and the breaking change
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` → all pass; `tools/test-postgres.sh` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>
