# Domain and Api layers

<block id="03-domain-editable-stale">
## TDDAB-3: Name the fields a client may write, and the stale version a read finds

<intro>
No dependency on 01-02. Two small pieces of `Zerox.Ef.Domain` the API needs (block 04):

- `EntityFields.Editable<T>()` — the NAMES of the fields a client may write: every public property with a
  public setter, except the key `Id`, the navigations and the reserved fields (`[JsonIgnore]`, not a
  navigation). The selection already exists in `EntityFields` (the private class `Editable<T>` used by
  `CopyEditable`): `Editable<T>()` returns its names, it is not selected a second time. The private class is
  renamed `Selection<T>` (a method and a nested type cannot share the name `Editable`). `CopyEditable` stays
  until block 04 removes its last consumer.
- `StaleVersionException` — thrown by an action that READS the current row (it needs its state) and finds
  that its version differs from the one the client read: the API answers 409 (block 04). Same shape as
  `InvalidRequestException` (three constructors, `///`), `public sealed`.

Files: `src/Zerox.Ef/Domain/EntityFields.cs`, `src/Zerox.Ef/Domain/StaleVersionException.cs` (new),
`tests/Zerox.Ef/Domain/EntityFieldsTests.cs`, `tests/Zerox.Ef/Domain/StaleVersionExceptionTests.cs` (new),
`docs/catalog.md` (an `EntityFields.Editable` row; a `StaleVersionException` row).

Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*EntityFieldsTests"` and `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*StaleVersionExceptionTests"`
</intro>

<red>
- test: Should_NameTheEditableFields_WhenAsked — `EntityFields.Editable<Item>()` contains `Name`, `Quantity`, `Note`, `Kind` and does not contain `Id`, `Box` (navigation) or `Secret` (reserved)
- test: Should_NameNoCollectionNavigation_WhenAsked — `EntityFields.Editable<Box>()` contains `Label` and does not contain `Items` or `Id`
- test: Should_NameTheFieldsCopyEditableWrites_WhenAsked — for `Item`, the names of `Editable<Item>()` are exactly the properties `CopyEditable` writes (one selection, not two)
- test: Should_CarryTheReason_WhenAStaleVersionIsThrown — `new StaleVersionException("Task 5 changed since it was read.").Message` is that text; the parameterless constructor has a non-empty message
</red>

### Implementation

```csharp
// src/Zerox.Ef/Domain/EntityFields.cs — the names, from the one selection
/// <summary>
/// The names of the fields a client may write on an entity it sends: every public field with a setter,
/// except the key, the navigations and the reserved fields — what IRepository.Update marks modified.
/// </summary>
public static IReadOnlyCollection<string> Editable<T>() where T : class, IEntity => Selection<T>.Names;

// CopyEditable reads Selection<T>.Properties until block 04 removes it.
private static class Selection<T>
{
    internal static readonly PropertyInfo[] Properties = /* the existing selection of Editable<T>, unchanged */;
    internal static readonly string[] Names = Properties.Select(p => p.Name).ToArray();
}
```

```csharp
// src/Zerox.Ef/Domain/StaleVersionException.cs
namespace Zerox.Ef.Domain;

/// <summary>
/// The row an action read has another version than the one the client read: the client decided on a row
/// that is no longer there. For an action that must read the current row before deciding (a state machine,
/// a rule on stored values): it compares the versions first and throws this. The API answers 409.
/// </summary>
public sealed class StaleVersionException : Exception
{
    /// <summary>
    /// A conflict with the generic reason.
    /// </summary>
    public StaleVersionException()
        : base("The row was changed after it was read.")
    {
    }

    /// <summary>
    /// A conflict with the reason.
    /// </summary>
    public StaleVersionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// A conflict with the reason and its cause.
    /// </summary>
    public StaleVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

<success>
- [ ] `EntityFields.Editable<T>()` returns the names of the one existing selection; `StaleVersionException` exists, documented with `///`
- [ ] `docs/catalog.md` lists both
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="04-api-client-version">
## TDDAB-4: Save, rename and delete through the API at the version the client read

<intro>
Depends on 02 (`IRepository<T>.Update`, `TryDeleteById`, `HasRowVersion`) and 03 (`EntityFields.Editable<T>()`,
`StaleVersionException`). The API stops reloading the row before a client's write:

- **PUT `{id}`**: 400 when the route and body ids differ (as today); `Repository.Update(entity,
  EntityFields.Editable<T>())`; `SaveUnlessMissingAsync(id)` → 404 when missing; else 200 with the row AS
  STORED (`Repository.Query().SingleAsync(e => e.Id == id)`, a no-tracking read: fresh version, the fields
  the body cannot carry), never the body.
- **DELETE `{id}?version=N`**: `version` binds `ulong?`; 400 (`InvalidRequestException`, message containing
  `?version=N`) when `Repository.HasRowVersion` and it is missing; `Repository.TryDeleteById(id, version)`
  false (the version does not fit the row version: a result, not an exception) → `InvalidRequestException`
  → 400; `SaveUnlessMissingAsync(id)` → 404 when missing, else 204.
- **`protected async Task<bool> SaveUnlessMissingAsync(int id, CancellationToken ct)`** on
  `EntityController<T>`: saves; on `DbUpdateConcurrencyException` reads `Repository.Query().AnyAsync(e =>
  e.Id == id)`: false → returns false (the caller answers 404), true → `throw;` (the middleware answers 409).
  The one place of the 404/409 split; a derived controller's action uses it too.
- **POST**: unchanged; its summary loses "starts at Version 0" (the version is the database's).
- **`ErrorHandlingMiddleware`**: `StaleVersionException` → 409 with the same detail and the same Warning log
  as a lock conflict.
- **`EntityFields.CopyEditable`** is removed (no consumer left) with its three tests in
  `tests/Zerox.Ef/Domain/EntityFieldsTests.cs`.
- **The Sample**: `Customer` gets `[Timestamp] public uint Version` (no `IHasOptimisticLock`);
  `RenameRequest(string Name, uint Version)`; `CustomersController.Rename` writes the client's one field:
  `Repository.Update(new Customer { Id = id, Name = body.Name, Version = body.Version }, [nameof(Customer.Name)])`,
  `SaveUnlessMissingAsync`, 404 or 200 with the stored row; `CustomersController` no longer takes
  `LastWriterWins`, and `SampleApp` no longer registers it.
- **`TestHost.Doc`** gets `[Timestamp] public uint Version` (no `IHasOptimisticLock`); `TestHost` uses the
  shared `LogCapture` (moved in block 01).
- **`openapi/Sample.json`** changes (Version and the `version` parameter types): `OpenApiDocumentTests`
  rewrites it on the first run and passes on the second; `ClientTests` regenerates the TypeScript client
  from it and its tests (`client/src/sample.test.ts`, which deletes with the version it read) pass unchanged.

Existing tests replaced, in `EntityControllerApiTests`: `Should_Update_WhenPut` → `Should_SaveWithTheVersionRead_WhenPut`,
`Should_StartAtVersionZero_WhenCreateCarriesAVersion` → `Should_IgnoreTheVersionOfACreation`,
`Should_Delete_WhenDeleted` → `Should_DeleteAtTheVersionRead_WhenDeleted`; the other tests keep their names and
are rewritten where the list below says so.

Tests read the version they send from the API (`GET api/Customers/{id}` → `Version`, `GetUInt32()`) through a
helper in each test class (`VersionOfAsync(string url)`), never a literal. Lock tests run on PostgreSQL only.

Files: `src/Zerox.Ef/Api/EntityController.cs`, `src/Zerox.Ef/Api/ErrorHandlingMiddleware.cs`,
`src/Zerox.Ef/Domain/EntityFields.cs`, `tests/Zerox.Ef/Domain/EntityFieldsTests.cs`,
`tests/Zerox.Ef/Api/TestHost.cs`, `tests/Zerox.Ef/Api/ErrorHandlingTests.cs` (endpoint `/stale` throwing
`StaleVersionException`), `tests/Zerox.Ef/Api/Sample/Db/Customer.cs`, `tests/Zerox.Ef/Api/Sample/Api/RenameRequest.cs`,
`tests/Zerox.Ef/Api/Sample/Api/CustomersController.cs`, `tests/Zerox.Ef/Api/Sample/Api/SampleApp.cs`,
`tests/Zerox.Ef/Api/Sample/EntityControllerApiTests.cs`, `tests/Zerox.Ef/Api/Sample/SampleApiTests.cs`,
`tests/Zerox.Ef/Api/Sample/ProblemDetailsTests.cs`, `tests/Zerox.Ef/Api/Sample/openapi/Sample.json`,
`docs/catalog.md` (EntityController, error handling, EntityFields rows), `README.md` (the Rename and DELETE lines).

Focused run: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-namespace "*Sample*"` · `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-class "*ErrorHandlingTests"` and `tools/test-postgres.sh --filter-namespace "*Api.Tests*"`
</intro>

<red>
- test: Should_SaveWithTheVersionRead_WhenPut — both providers: the version read with GET, PUT with it → 200, the body is the stored row (new Name, stored Version)
- test: Should_ChangeTheVersion_WhenPut — PostgreSQL only: the Version answered by the PUT differs from the one sent
- test: Should_KeepTheReservedFields_WhenPut — both providers: PUT of an `Order` without `InternalNote` → 200, the stored `InternalNote` is still "internal"
- test: Should_Answer404_WhenPutAMissingId — both providers, `api/Orders/999` and `api/Customers/999` (with a version) → 404
- test: Should_IgnoreTheVersionOfACreation — both providers: POST a customer with `"Version":99` → 201, the answered Version equals the stored one and is not 99
- test: Should_DeleteAtTheVersionRead_WhenDeleted — both providers: POST, the version read with GET, DELETE `?version=` it → 204, then GET → 404
- test: Should_Answer404_WhenDeletingAMissingRow — both providers: DELETE `api/Customers/999?version=0` and `api/Orders/999` → 404
- test: Should_Answer400_WhenDeletingALockedRowWithoutItsVersion — both providers: DELETE `api/Customers/2` → 400, the detail contains `version=`
- test: Should_Answer400_WhenTheVersionDoesNotFitTheRowVersion — both providers: DELETE `api/Customers/1?version=18446744073709551615` → 400 `InvalidRequest`
- test: Should_Answer409_WhenPutCarriesAStaleVersion — PostgreSQL only: two clients read the same version, the first PUT → 200, the second → 409 `Conflict`, the stored Name is the first's
- test: Should_Answer409_WhenDeleteCarriesAStaleVersion — PostgreSQL only: the version read, a PUT changes the row, DELETE `?version=` the old one → 409, the row is still there
- test: Should_RenameOnlyTheName_WhenRenamed — both providers: PUT `{id}/name` with the version read → 200, the answered and stored City is unchanged, Name is the new one
- test: Should_Answer404_WhenRenamingAMissingCustomer — both providers: PUT `api/Customers/404/name` → 404
- test: Should_Answer409_WhenARenameCarriesAStaleVersion — PostgreSQL only: two clients rename with the same version read → 200 then 409, the stored Name is the first's
- test: Should_Answer409_WhenAFullPutOrARenameIsStale — PostgreSQL only: after a rename, a PUT and a rename with the version read before it → both 409
- test: Should_ShowTheWholeLifecycle — both providers: the existing lifecycle, the rename sending the version read with GET
- test: Should_Answer409_WhenAStaleVersionIsThrown — both providers: `/stale` (throws `StaleVersionException`) → 409, `Code` = `Conflict`, a non-empty detail
- test: Should_LogAWarning_WhenAConflictHappens — both providers: after `/stale` the middleware logged a Warning and no Error (it used `/conflict`, which conflicts only on PostgreSQL now)
- test: Should_Answer409_WhenALockedRowWasChanged — PostgreSQL only: `/conflict` (a real stale save of `Doc`) → 409 `Conflict`
- test: Should_AnswerAProblemDetailsWithTheCode_ForEveryFailure — both providers: the existing cases, plus DELETE `api/Customers/1?version=18446744073709551615` → 400 `InvalidRequest`
</red>

### Implementation

```csharp
// src/Zerox.Ef/Api/EntityController.cs — PUT, DELETE and the split
/// <summary>
/// The row as the client sent it, at the version it read: only the editable fields are written (never the
/// key, the navigations, the reserved fields or what the database generates), without reading the row
/// first. 200 with the row as stored; 404 when it is missing; a row changed since the client read it
/// answers 409 (the middleware).
/// </summary>
[HttpPut("{id:int}")]
public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(entity);
    if (id != entity.Id)
        throw new InvalidRequestException("The id in the route and in the body differ.");
    Repository.Update(entity, EntityFields.Editable<T>());
    if (!await SaveUnlessMissingAsync(id, ct).ConfigureAwait(false))
        return NotFound();
    return Ok(await Repository.Query().SingleAsync(e => e.Id == id, ct).ConfigureAwait(false));
}

/// <summary>
/// Deletes the row at the version the client read (?version=N, required when the entity has a row
/// version): 204; 404 when it is missing; 409 when it changed since — a delete must not destroy a change
/// the client never saw.
/// </summary>
[HttpDelete("{id:int}")]
public virtual async Task<IActionResult> Delete(int id, [FromQuery] ulong? version, CancellationToken ct)
{
    if (Repository.HasRowVersion && version is null)
        throw new InvalidRequestException($"{typeof(T).Name} has an optimistic lock: give the Version you read, as ?version=N.");
    if (!Repository.TryDeleteById(id, version))
        throw new InvalidRequestException($"{version} is not a version of {typeof(T).Name}: give the Version you read.");
    return await SaveUnlessMissingAsync(id, ct).ConfigureAwait(false) ? NoContent() : NotFound();
}

/// <summary>
/// Saves a client's write. An UPDATE or DELETE that hit no row fails the same way whether the row is
/// missing or changed since the client read it; only then one read tells them apart: false when the row
/// is missing (the caller answers 404), the conflict goes on when it exists (the middleware answers 409).
/// </summary>
protected async Task<bool> SaveUnlessMissingAsync(int id, CancellationToken ct)
{
    try
    {
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
    catch (DbUpdateConcurrencyException)
    {
        if (await Repository.Query().AnyAsync(e => e.Id == id, ct).ConfigureAwait(false))
            throw;
        return false;
    }
}
```

```csharp
// src/Zerox.Ef/Api/ErrorHandlingMiddleware.cs — the added catch, before InvalidRequestException
catch (StaleVersionException e)
{
    logger.LogWarning(e, "Optimistic lock conflict");
    await WriteAsync(context, StatusCodes.Status409Conflict, "The row was changed by another user.").ConfigureAwait(false);
}
```

```csharp
// tests/Zerox.Ef/Api/Sample/Api/CustomersController.cs — one field at the version read
public sealed class CustomersController(IRepository<Customer> repository, ICustomerMerge merge)
    : EntityController<Customer>(repository)
{
    // MergeInto unchanged.

    // One field, at the version the client read, without reading the row: a stale one answers 409, like a full PUT.
    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        Repository.Update(new Customer { Id = id, Name = body.Name, Version = body.Version }, [nameof(Customer.Name)]);
        if (!await SaveUnlessMissingAsync(id, ct))
            return NotFound();
        return Ok(await Repository.Query().SingleAsync(c => c.Id == id, ct));
    }
}
```

```csharp
// tests/Zerox.Ef/Api/Sample/EntityControllerApiTests.cs — reference tests
private async Task<uint> VersionOfAsync(string url) =>
    (await SendAsync(HttpMethod.Get, url)).Body.GetProperty("Version").GetUInt32();

[Fact]
public async Task Should_Answer409_WhenPutCarriesAStaleVersion()
{
    Assert.SkipUnless(TestDatabase.OnPostgres, "SQLite has no row version: the lock is proved on PostgreSQL.");
    var read = await VersionOfAsync("api/Customers/2");   // both clients read it now
    var first = await SendAsync(HttpMethod.Put, "api/Customers/2", $$"""{"Id":2,"Name":"Bolt One","City":"Milan","Version":{{read}}}""");

    var second = await SendAsync(HttpMethod.Put, "api/Customers/2", $$"""{"Id":2,"Name":"Bolt Two","City":"Milan","Version":{{read}}}""");

    first.Status.Should().Be(HttpStatusCode.OK);
    second.Status.Should().Be(HttpStatusCode.Conflict);
    second.Body.GetProperty("Code").GetString().Should().Be("Conflict");
    await using var check = App.NewContext();
    (await check.Customers.SingleAsync(c => c.Id == 2, Ct)).Name.Should().Be("Bolt One");
}
```

<success>
- [ ] PUT and DELETE never read the row before writing; 404 and 409 are told apart only after a failed save, in `SaveUnlessMissingAsync`; the PUT answers the row as stored
- [ ] `StaleVersionException` answers 409 like a lock conflict; `EntityFields.CopyEditable` is gone
- [ ] On PostgreSQL a stale PUT, rename or DELETE answers 409 and the stored row is kept; `openapi/Sample.json` and the TypeScript client follow the new types and `ClientTests` pass
- [ ] `docs/catalog.md` and `README.md` describe PUT/DELETE/Rename at the version read
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` · `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` · `tools/test-postgres.sh` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="05-remove-app-managed-lock">
## TDDAB-5: Remove the application-managed version and the pin

<intro>
Depends on 04 (no consumer of `PinReadVersion` or `IHasOptimisticLock` is left but `LastWriterWins`' client
version and the Domain tests' `Memo`). The application-managed lock goes entirely:

- `src/Zerox.Ef/Core/IHasOptimisticLock.cs` — deleted.
- `IRepository<T>.PinReadVersion` and `Repository<T>.PinReadVersion` — deleted.
- `SessionInterceptor.SavingChangesAsync` — the loop that wrote `Version = 0` / `Version++` and its comment
  are deleted (the massive-op refusal and the unit-of-work swallow stay as they are).
- `ModelBuilderExtensions.ApplyOptimisticLock` — the `IHasOptimisticLock` branch is deleted; its summary
  speaks only of row versions.
- `LastWriterWins.SaveAsync<T>(int id, Expression<Func<T, bool>> scope, Func<T, CancellationToken, Task> apply,
  CancellationToken ct = default)` — the `versionReadByClient` parameter and its pin are deleted: it is for a
  change the SERVER decides alone (re-read and re-apply on conflict, the last writer wins, `MaxAttempts` 3,
  null = missing or out of scope). Its summary says so, and says that a client's change at the version it
  read is `IRepository<T>.Update(new T { Id, Field, Version }, [nameof(T.Field)])`.
- `tests/Zerox.Ef/Domain/TestDb.cs` — `Memo` gets `[Timestamp] public uint Version` (no `IHasOptimisticLock`).
- `tests/Zerox.Ef/Domain/LastWriterWinsTests.cs` — the three client-version tests are deleted
  (`Should_ThrowWithoutRetry_WhenTheClientVersionIsStale`, `Should_ThrowWithoutRetry_WhenAnotherWriterSavesAfterTheClientRead`,
  `Should_Save_WhenTheClientVersionIsCurrent`); the conflict tests run on PostgreSQL only; no test asserts a
  version number.
- `tests/Zerox.Ef/Domain/SurfaceTests.cs` (new) — the surface has no application-managed lock.
- `docs/catalog.md` — the `IHasOptimisticLock` row becomes the row-version row (`[Timestamp]` +
  `ApplyOptimisticLock(Database)` + `IRepository<T>.Update/TryDeleteById/HasRowVersion`); the `LastWriterWins`
  row speaks only of the server's own change. `README.md` — the database-foundation walk-through has no
  `IHasOptimisticLock`, `PinReadVersion` or client version in `LastWriterWins`.

Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*SurfaceTests"` · `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*LastWriterWinsTests"` and `tools/test-postgres.sh --filter-class "*LastWriterWinsTests"`
</intro>

<red>
- test: Should_ExposeNoVersionType_WhenTheCoreIsInspected — the `Zerox.Ef.Core` assembly (`typeof(IEntity).Assembly`) has no type `Zerox.Ef.Core.IHasOptimisticLock`
- test: Should_ExposeNoPin_WhenTheRepositoryIsInspected — `typeof(IRepository<>)` has no method `PinReadVersion`
- test: Should_TakeNoClientVersion_WhenLastWriterWinsIsInspected — `LastWriterWins.SaveAsync` has no parameter named `versionReadByClient`
- test: Should_RunOnce_WhenThereIsNoConflict — both providers: one call, the saved Text is "mine"
- test: Should_ReReadAndSave_WhenAConflictHappens — PostgreSQL only: another writer saves during the first attempt → two calls, the row saved
- test: Should_KeepTheLastWritersValues_AfterTheRetry — PostgreSQL only: after the retry Text is "mine" and Note is the other writer's
- test: Should_StoreANewVersion_WhenTheRetrySaves — PostgreSQL only: the stored Version after the retry differs from the one the other writer left
- test: Should_Throw_WhenAttemptsAreExhausted — PostgreSQL only: a conflict on every attempt → `DbUpdateConcurrencyException` after `MaxAttempts` calls
- test: Should_ReturnNullWithoutRetry_WhenTheRowDisappears — both providers: the row deleted during the attempt → null, one call
- test: Should_NotOpenATransaction — both providers: no transaction during the apply
- test: Should_NotTouchARowOutsideTheScope — both providers: a row outside the scope → null, no call
</red>

### Implementation

```csharp
// tests/Zerox.Ef/Domain/SurfaceTests.cs
using System.Reflection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain.Tests;

// The lock is the database's row version: nothing of the application writes a version.
public sealed class SurfaceTests
{
    [Fact]
    public void Should_ExposeNoVersionType_WhenTheCoreIsInspected() =>
        typeof(IEntity).Assembly.GetType("Zerox.Ef.Core.IHasOptimisticLock").Should().BeNull();

    [Fact]
    public void Should_ExposeNoPin_WhenTheRepositoryIsInspected() =>
        typeof(IRepository<>).GetMethod("PinReadVersion").Should().BeNull();

    [Fact]
    public void Should_TakeNoClientVersion_WhenLastWriterWinsIsInspected() =>
        typeof(LastWriterWins).GetMethod(nameof(LastWriterWins.SaveAsync))!.GetParameters()
            .Should().NotContain(p => p.Name == "versionReadByClient");
}
```

```csharp
// src/Zerox.Ef/Domain/LastWriterWins.cs — the server's own change only
/// <summary>
/// For a change the SERVER decides alone on ONE row (no client version): on a conflict the row is read
/// again and the change applied again, so the last writer wins; after MaxAttempts the conflict reaches the
/// caller. A client's change at the version it read is not this: it is IRepository.Update with that
/// version, refused when stale. No lock is ever taken, no explicit transaction: only saves.
/// </summary>
public async Task<T?> SaveAsync<T>(int id, Expression<Func<T, bool>> scope, Func<T, CancellationToken, Task> apply,
    CancellationToken ct = default) where T : class, IEntity
{
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(apply);
    var repository = repositories.For<T>();
    for (var attempt = 0; ; attempt++)
    {
        var entity = await repository.Query(tracked: true).Where(scope)
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
            return null;
        await apply(entity, ct).ConfigureAwait(false);
        try
        {
            await repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return entity;
        }
        catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts - 1)
        {
            // Retrying IS the purpose; the stale copy is thrown away so the next attempt reads the database.
            repository.DetachAll();
        }
    }
}
```

```csharp
// src/Zerox.Ef/Db/SessionInterceptor.cs — SavingChangesAsync without the version
var session = DbSession.Of(eventData.Context!);
if (session.Massive is not null)
    throw new InvalidOperationException("A massive operation is open: no repository can save until it is closed.");
// Inside a unit of work the save is swallowed: the unit of work writes, once.
if (session.Boundary is not null)
    return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
return ValueTask.FromResult(result);
```

<success>
- [ ] `IHasOptimisticLock`, `PinReadVersion`, the interceptor's version code and `LastWriterWins`' client version are gone; nothing in `src/` writes a version
- [ ] `LastWriterWins` retries the server's own change on PostgreSQL; its tests assert no version number
- [ ] `docs/catalog.md` and `README.md` describe only the database's row version
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` · `dotnet test --project tests/Zerox.Ef/Db/Zerox.Ef.Db.Tests.csproj` · `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj` · `tools/test-postgres.sh` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>
