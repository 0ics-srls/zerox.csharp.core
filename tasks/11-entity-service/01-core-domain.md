<block id="01-core-contracts">
## TDDAB-1: Declare the outcome, the load types and the service contract in Zerox.Ef.Core

<intro>
Creates the contracts every later block uses, in `Zerox.Ef.Core` (BCL only, no package):
- `src/Zerox.Ef/Core/OpResult.cs` — `OpStatus`, `OpResult<T>`, the static class `OpResult` with the factories;
- `src/Zerox.Ef/Core/LoadOptions.cs` — `LoadOptions`, `SortingInfo`, `GroupingInfo`, `SummaryInfo`: every member
  of DevExtreme 5.1.0's `DataSourceLoadOptionsBase` (and of its `SortingInfo`, `GroupingInfo`, `SummaryInfo`)
  with the same name and the same meaning, so the JSON a client sends today binds unchanged;
- `src/Zerox.Ef/Core/Loaded.cs` — `Loaded` and `LoadGroup`, written in JSON exactly as DevExtreme's
  `LoadResult` and `Group` (measured on 5.1.0): `data`, `totalCount`, `groupCount` always written
  (`-1` when not asked), `summary` left out when null; a group's `key`, `items` always, `count` and `summary`
  left out when null;
- `src/Zerox.Ef/Core/IEntityService.cs` — `IEntityService<T>`.
Tests go to `tests/Zerox.Ef/Domain` (the Ef test project that references Core): `OpResultTests.cs`,
`LoadTypesTests.cs`, one test added to `SurfaceTests.cs`.
Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*OpResultTests" --filter-class "*LoadTypesTests" --filter-class "*SurfaceTests"`.
</intro>

<red>
- test: Should_CarryTheValue_WhenOk — `OpResult.Ok(42)` has Status Ok, Value 42, Reason null, Exception null, IsSuccess true
- test: Should_CarryTheValue_WhenCreated — `OpResult.Created("x")` has Status Created, Value "x", IsSuccess true
- test: Should_CarryNoValue_WhenDeleted — `OpResult.Deleted<string>()` has Status Deleted, Value null, IsSuccess true
- test: Should_CarryNoValue_WhenNotFound — `OpResult.NotFound<string>()` has Status NotFound, Value null, IsSuccess false
- test: Should_CarryTheReason_WhenConflictInvalidOrFailed — Theory over Conflict, Invalid, Failed: the reason given is the Reason, IsSuccess false
- test: Should_RefuseABlankReason_WhenConflictInvalidOrFailed — Theory over the three factories with "" and "  ": ArgumentException
- test: Should_CarryTheException_WhenFaulted — `OpResult.Faulted<int>(e)` has Status Faulted, Exception the same instance, Reason e.Message, IsSuccess false
- test: Should_RefuseANullException_WhenFaulted — `OpResult.Faulted<int>(null!)`: ArgumentNullException
- test: Should_BeSuccessOnlyForOkCreatedDeleted — every `Enum.GetValues<OpStatus>()`: IsSuccess is true exactly for Ok, Created, Deleted
- test: Should_BindTheOptionsAClientSendsToday — `{"Skip":10,"Take":5,"RequireTotalCount":true,"Filter":["Kind","=","Large"],"Sort":[{"Selector":"Quantity","Desc":true}],"Group":[{"Selector":"At","GroupInterval":"month","IsExpanded":false}],"TotalSummary":[{"Selector":"Quantity","SummaryType":"sum"}],"Select":["Name"]}` deserialized with naming policy null gives those values (Filter has 3 elements, Sort[0].Desc true, Group[0].GroupInterval "month", TotalSummary[0].SummaryType "sum", Select ["Name"])
- test: Should_WriteTheResultWithDevExtremeNames — `new Loaded { Data = [1, 2], TotalCount = 2, GroupCount = -1 }` serialized with naming policy null is exactly `{"data":[1,2],"totalCount":2,"groupCount":-1}` (no summary)
- test: Should_WriteAGroupWithDevExtremeNames — `new LoadGroup { Key = "a", Items = null, Count = 3, Summary = [6] }` serializes to `{"key":"a","items":null,"count":3,"summary":[6]}`; with Count and Summary null to `{"key":"a","items":null}`
- test: Should_ReferenceOnlyTheRuntime_WhenTheCoreIsInspected (SurfaceTests) — every assembly `typeof(IEntity).Assembly` references is named `System*`, `netstandard` or `mscorlib`
</red>

### Implementation

```csharp
// src/Zerox.Ef/Core/OpResult.cs
namespace Zerox.Ef.Core;

/// <summary>What became of an operation. No transport meaning: the API translates it, a UI switches on it.</summary>
public enum OpStatus
{
    /// <summary>Done; the value is the result.</summary>
    Ok,
    /// <summary>A new row exists; the value is it.</summary>
    Created,
    /// <summary>The row is gone; no value.</summary>
    Deleted,
    /// <summary>No row with that id.</summary>
    NotFound,
    /// <summary>The row changed after the caller read it.</summary>
    Conflict,
    /// <summary>The request cannot be executed as given; the reason says why.</summary>
    Invalid,
    /// <summary>The domain refused it in the current state; the reason says why.</summary>
    Failed,
    /// <summary>An exception nobody expected; it is inside.</summary>
    Faulted,
}

/// <summary>The outcome of an operation: its status, its value, why it did not succeed, the exception when one happened.</summary>
public sealed record OpResult<T>(OpStatus Status, T? Value, string? Reason, Exception? Exception)
{
    /// <summary>Ok, Created or Deleted.</summary>
    public bool IsSuccess => Status is OpStatus.Ok or OpStatus.Created or OpStatus.Deleted;
}

/// <summary>The ways to build an <see cref="OpResult{T}"/>.</summary>
public static class OpResult
{
    public static OpResult<T> Ok<T>(T value) => new(OpStatus.Ok, value, null, null);
    public static OpResult<T> Created<T>(T value) => new(OpStatus.Created, value, null, null);
    public static OpResult<T> Deleted<T>() => new(OpStatus.Deleted, default, null, null);
    public static OpResult<T> NotFound<T>() => new(OpStatus.NotFound, default, null, null);
    public static OpResult<T> Conflict<T>(string reason) => WithReason<T>(OpStatus.Conflict, reason);
    public static OpResult<T> Invalid<T>(string reason) => WithReason<T>(OpStatus.Invalid, reason);
    public static OpResult<T> Failed<T>(string reason) => WithReason<T>(OpStatus.Failed, reason);
    public static OpResult<T> Faulted<T>(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(OpStatus.Faulted, default, exception.Message, exception);
    }

    private static OpResult<T> WithReason<T>(OpStatus status, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(status, default, reason, null);
    }
}
```
(every public member carries its `///`; shown on the types only to keep the plan short)

```csharp
// src/Zerox.Ef/Core/LoadOptions.cs — DevExtreme's DataSourceLoadOptionsBase member by member (5.1.0)
namespace Zerox.Ef.Core;

public sealed class LoadOptions
{
    public bool RequireTotalCount { get; set; }
    public bool RequireGroupCount { get; set; }
    public bool IsCountQuery { get; set; }
    public bool IsSummaryQuery { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public IReadOnlyList<SortingInfo>? Sort { get; set; }
    public IReadOnlyList<GroupingInfo>? Group { get; set; }
    public IReadOnlyList<object?>? Filter { get; set; }      // DevExtreme's array form, nested arrays inside
    public IReadOnlyList<SummaryInfo>? TotalSummary { get; set; }
    public IReadOnlyList<SummaryInfo>? GroupSummary { get; set; }
    public IReadOnlyList<string>? Select { get; set; }
    public IReadOnlyList<string>? PreSelect { get; set; }
    public bool? RemoteSelect { get; set; }
    public bool? RemoteGrouping { get; set; }
    public bool? ExpandLinqSumType { get; set; }
    public IReadOnlyList<string>? PrimaryKey { get; set; }
    public string? DefaultSort { get; set; }
    public bool? StringToLower { get; set; }
    public bool? PaginateViaPrimaryKey { get; set; }
    public bool? SortByPrimaryKey { get; set; }
    public bool AllowAsyncOverSync { get; set; }
}

public sealed class SortingInfo
{
    public string? Selector { get; set; }
    public bool Desc { get; set; }
}

public sealed class GroupingInfo
{
    public string? Selector { get; set; }
    public bool Desc { get; set; }
    public string? GroupInterval { get; set; }
    public bool? IsExpanded { get; set; }
}

public sealed class SummaryInfo
{
    public string? Selector { get; set; }
    public string? SummaryType { get; set; }
}
```

```csharp
// src/Zerox.Ef/Core/Loaded.cs
using System.Text.Json.Serialization;

namespace Zerox.Ef.Core;

/// <summary>What a load answers: rows (whole or the selected columns) or groups, the counts asked, the summary.</summary>
public sealed class Loaded
{
    [JsonPropertyName("data")] public IReadOnlyList<object> Data { get; init; } = [];
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; } = -1;
    [JsonPropertyName("groupCount")] public int GroupCount { get; init; } = -1;
    [JsonPropertyName("summary"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object?>? Summary { get; init; }
}

/// <summary>One group of a load: its key, the inner level's groups (null at the last level), how many rows, its sums.</summary>
public sealed class LoadGroup
{
    [JsonPropertyName("key")] public object? Key { get; init; }
    [JsonPropertyName("items")] public IReadOnlyList<LoadGroup>? Items { get; init; }
    [JsonPropertyName("count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Count { get; init; }
    [JsonPropertyName("summary"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<object?>? Summary { get; init; }
}
```

```csharp
// src/Zerox.Ef/Core/IEntityService.cs
namespace Zerox.Ef.Core;

/// <summary>
/// The operations every entity has, for any caller — the API, an in-process UI. Never throws for an outcome:
/// every one, an unexpected exception included, comes back as an <see cref="OpResult{T}"/>.
/// </summary>
public interface IEntityService<T> where T : class, IEntity
{
    /// <summary>The one search: rows, the asked columns or groups, as the options decide (Invalid with the reason when they cannot run).</summary>
    Task<OpResult<Loaded>> LoadAsync(LoadOptions options, CancellationToken ct = default);
    /// <summary>The distinct values of a readable text column (Invalid for any other name).</summary>
    Task<OpResult<IReadOnlyList<string>>> ValuesAsync(string column, CancellationToken ct = default);
    /// <summary>The row with that id: Ok, or NotFound.</summary>
    Task<OpResult<T>> GetAsync(int id, CancellationToken ct = default);
    /// <summary>Adds the row: Created with it (its Id and row version the database's); Invalid when it carries an Id.</summary>
    Task<OpResult<T>> CreateAsync(T entity, CancellationToken ct = default);
    /// <summary>Writes the editable fields of the caller's row at the version it read: Ok with the row as stored, NotFound, Conflict.</summary>
    Task<OpResult<T>> UpdateAsync(T entity, CancellationToken ct = default);
    /// <summary>Deletes the row at the version the caller read: Deleted, NotFound, Conflict; Invalid when a locked entity gets no version or one that does not fit.</summary>
    Task<OpResult<T>> DeleteAsync(int id, ulong? version, CancellationToken ct = default);
}
```

```csharp
// tests/Zerox.Ef/Domain/LoadTypesTests.cs — the JSON settings of the API: names as declared
private static readonly JsonSerializerOptions AsIs = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null };

[Fact]
public void Should_WriteTheResultWithDevExtremeNames()
{
    var loaded = new Loaded { Data = [1, 2], TotalCount = 2, GroupCount = -1 };

    var json = JsonSerializer.Serialize(loaded, AsIs);

    json.Should().Be("""{"data":[1,2],"totalCount":2,"groupCount":-1}""");
}
```

`docs/catalog.md`: a row in `## Zerox.Ef.Core` for `OpResult<T>` / `OpStatus` / `OpResult` (`Zerox.Ef/Core/OpResult.cs`), one for
the load types (`LoadOptions`, `SortingInfo`, `GroupingInfo`, `SummaryInfo`, `Loaded`, `LoadGroup`), one for
`IEntityService<T>` (implemented by block 03).

<success>
- [ ] `Zerox.Ef.Core` has `OpStatus`, `OpResult<T>`, `OpResult`, the load types and `IEntityService<T>`, every public member documented
- [ ] `Zerox.Ef.Core.csproj` still has no reference (no package, no project)
- [ ] `docs/catalog.md` lists the three new rows
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="02-domain-load-conversion">
## TDDAB-2: Convert the own load types to DevExtreme's and back

<intro>
Creates `src/Zerox.Ef/Domain/LoadConversion.cs`, `internal static class LoadConversion`:
- `ToDevExtreme(LoadOptions)` → `DevExtreme.AspNet.Data.DataSourceLoadOptionsBase`, every member copied by
  name (`Sort`/`Group`/`TotalSummary`/`GroupSummary` into DevExtreme's `SortingInfo`/`GroupingInfo`/`SummaryInfo`
  arrays, `Select`/`PreSelect`/`PrimaryKey` into `string[]`, `Filter` into a `List<object?>` with the same
  elements); `Search.MakeSafe` stays the place that overrides what the server decides;
- `ToLoaded(DevExtreme.AspNet.Data.ResponseModel.LoadResult)` → `Loaded`: `data` copied (an empty list when
  null), every `DevExtreme.AspNet.Data.ResponseModel.Group` (`key`, `items` = `IList` of inner groups or null,
  `count` = `int?`, `summary` = `object[]`) turned into a `LoadGroup` recursively, any other row kept as it is;
  `totalCount`, `groupCount`, `summary` copied.
The Core and DevExtreme types share names: the file aliases DevExtreme's
(`using DxSorting = DevExtreme.AspNet.Data.SortingInfo;` and so on). Uses block 01's types.
Tests: `tests/Zerox.Ef/Domain/LoadConversionTests.cs` (reaches the internal class through
`InternalsVisibleTo`), on `TestDb` with the 30-item seed of `SearchTests` copied (Quantity 1..30, Kind Small
for odd and Large for even, At January for 1..10, February for the rest, red box 1..15, blue box 16..30).
Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*LoadConversionTests"`.
</intro>

<red>
- test: Should_HaveEveryMemberOfDevExtreme_WhenLoadOptionsIsInspected — every public instance property name of `DataSourceLoadOptionsBase` is a public property of `LoadOptions` (and the same for `SortingInfo`, `GroupingInfo` — its own and inherited —, `SummaryInfo`)
- test: Should_CopyEveryMember_WhenConvertedToDevExtreme — a `LoadOptions` with every member set to a non-default value converts to a `DataSourceLoadOptionsBase` whose properties, compared by name through reflection (arrays element by element, Sort/Group/Summary by their members), equal them
- test: Should_LeaveTheCollectionsNull_WhenNotGiven — `new LoadOptions()` converts with Sort, Group, Filter, TotalSummary, GroupSummary, Select, PreSelect, PrimaryKey null
- test: Should_KeepTheRows_WhenConvertedBack — `Search.LoadAsync` with `ToDevExtreme(new LoadOptions { Take = 5, Sort = [new() { Selector = "Quantity" }] })`, then `ToLoaded`: `Data` holds the same 5 `Item` instances, quantities 1..5
- test: Should_TurnGroupsIntoLoadGroups_WhenConvertedBack — Group by Kind with GroupSummary sum of Quantity: two `LoadGroup`, Small has Count 15, Summary [225], Items null
- test: Should_NestTheGroups_WhenConvertedBack — Group by Kind then `Box.Label`: each outer `LoadGroup` has 2 inner `LoadGroup` items, their counts sum to 30
- test: Should_CopyCountsAndSummary_WhenConvertedBack — `IsSummaryQuery`, `RequireTotalCount`, `TotalSummary` sum of Quantity: `Data` empty, `TotalCount` 30, `Summary` [465]
</red>

### Implementation

```csharp
// src/Zerox.Ef/Domain/LoadConversion.cs
using System.Collections;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Zerox.Ef.Core;
using DxGroup = DevExtreme.AspNet.Data.ResponseModel.Group;
using DxGrouping = DevExtreme.AspNet.Data.GroupingInfo;
using DxSorting = DevExtreme.AspNet.Data.SortingInfo;
using DxSummary = DevExtreme.AspNet.Data.SummaryInfo;
using Loaded = Zerox.Ef.Core.Loaded;

namespace Zerox.Ef.Domain;

// The own load types of Zerox.Ef.Core ↔ DevExtreme's: the search runs on DevExtreme, the contracts never name it.
internal static class LoadConversion
{
    public static DataSourceLoadOptionsBase ToDevExtreme(LoadOptions options) => new()
    {
        RequireTotalCount = options.RequireTotalCount,
        RequireGroupCount = options.RequireGroupCount,
        IsCountQuery = options.IsCountQuery,
        IsSummaryQuery = options.IsSummaryQuery,
        Skip = options.Skip,
        Take = options.Take,
        Sort = options.Sort?.Select(s => new DxSorting { Selector = s.Selector, Desc = s.Desc }).ToArray(),
        Group = options.Group?.Select(g => new DxGrouping { Selector = g.Selector, Desc = g.Desc, GroupInterval = g.GroupInterval, IsExpanded = g.IsExpanded }).ToArray(),
        Filter = options.Filter?.ToList(),
        TotalSummary = options.TotalSummary?.Select(Summary).ToArray(),
        GroupSummary = options.GroupSummary?.Select(Summary).ToArray(),
        Select = options.Select?.ToArray(),
        PreSelect = options.PreSelect?.ToArray(),
        RemoteSelect = options.RemoteSelect,
        RemoteGrouping = options.RemoteGrouping,
        ExpandLinqSumType = options.ExpandLinqSumType,
        PrimaryKey = options.PrimaryKey?.ToArray(),
        DefaultSort = options.DefaultSort,
        StringToLower = options.StringToLower,
        PaginateViaPrimaryKey = options.PaginateViaPrimaryKey,
        SortByPrimaryKey = options.SortByPrimaryKey,
        AllowAsyncOverSync = options.AllowAsyncOverSync,
    };

    public static Loaded ToLoaded(LoadResult result) => new()
    {
        Data = (result.data?.Cast<object>() ?? []).Select(Row).ToList(),
        TotalCount = result.totalCount,
        GroupCount = result.groupCount,
        Summary = result.summary,
    };

    private static DxSummary Summary(Core.SummaryInfo s) => new() { Selector = s.Selector, SummaryType = s.SummaryType };

    private static object Row(object row) => row is DxGroup group ? Group(group) : row;

    private static LoadGroup Group(DxGroup group) => new()
    {
        Key = group.key,
        Items = group.items?.Cast<DxGroup>().Select(Group).ToList(),
        Count = group.count,
        Summary = group.summary,
    };
}
```

<success>
- [ ] `LoadConversion` converts every member both ways; groups become `LoadGroup` at every level
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="03-domain-entity-service">
## TDDAB-3: Run every entity operation through EntityService<T>, outcomes as results

<intro>
Creates in `Zerox.Ef.Domain`:
- `src/Zerox.Ef/Domain/OptimisticLockConflict.cs` — `public static class OptimisticLockConflict` with
  `public static bool Is(DbUpdateConcurrencyException e)`: true when an entry of the failed save has a
  concurrency token — the body of `ErrorHandlingMiddleware.IsOptimisticLockConflict`
  (`e.Entries.Any(entry => entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken))`), which block 06
  replaces with a call to this one. (EF throws `DbUpdateConcurrencyException` on every UPDATE/DELETE that hits 0
  rows, even without a token: the conflict belongs only to the opt-in lock.)
- `src/Zerox.Ef/Domain/EntityService.cs` — `public class EntityService<T>(IRepository<T> repository) :
  IEntityService<T> where T : class, IEntity`, derivable, implementing `IEntityService<T>` of block 01 with the
  existing capabilities, never rewritten: `Search.LoadAsync` (via `LoadConversion` of block 02),
  `DistinctTextValues.IsTextColumn<T>` / `ValuesAsync`, `EntityFields.Editable<T>()`,
  `IRepository<T>.Update` / `TryDeleteById` / `HasRowVersion` / `AddAsync` / `GetByIdAsync` / `Query` /
  `SaveChangesAsync`. The behaviour of each operation is today's `EntityController<T>` action
  (`src/Zerox.Ef/Api/EntityController.cs`) with every answer a status instead of an HTTP result or an
  exception. `SaveUnlessMissingAsync` moves here unchanged (protected); `EntityController<T>` keeps its own
  copy until block 06 removes it.
- `RunAsync` is the one place where an exception becomes a status, used by every operation of the base and of a
  derived service: `InvalidRequestException` → Invalid (its message), `StaleVersionException` → Conflict (its
  message), `DbUpdateConcurrencyException` for which `OptimisticLockConflict.Is` → Conflict ("The row was changed
  by another user."), **any other exception** → Faulted with it (owner: "all of them", cancellation included).
Tests: `tests/Zerox.Ef/Domain/EntityServiceTests.cs` on `TestDb` (`NewScope()` gives `IRepository<Item>` /
`IRepository<Memo>` of `AddZeroxDb<TestDbContext>`; `Memo` has `[Timestamp] uint Version`), a nested
`private sealed class Probe(IRepository<Item> r) : EntityService<Item>(r)` exposing `RunAsync` to the tests,
`tests/Zerox.Ef/Domain/OptimisticLockConflictTests.cs`. Conflict tests run on PostgreSQL only.
Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*EntityServiceTests" --filter-class "*OptimisticLockConflictTests"`;
on PostgreSQL `tools/test-postgres.sh --filter-class "*EntityServiceTests"`.
</intro>

<red>
- test: Should_LoadThePage_WhenTheOptionsAreValid — 30 seeded items, `LoadAsync(new LoadOptions { Take = 5, Sort = [new() { Selector = "Quantity", Desc = true }], RequireTotalCount = true })`: Ok, `Data` quantities 30..26, `TotalCount` 30
- test: Should_ReturnInvalidWithTheReason_WhenRowsAreAskedWithoutTake — `LoadAsync(new LoadOptions())`: Invalid, Reason contains "Take"
- test: Should_ReturnInvalid_WhenTakeIsAboveMaxTake — a `Probe` whose `MaxTake` override is 10, Take 11: Invalid, Reason contains "10"
- test: Should_LoadOnlyTheSearchSource_WhenItIsOverridden — a derived service whose `SearchSource()` keeps Quantity ≤ 3, Take 50, RequireTotalCount: TotalCount 3
- test: Should_ReturnTheDistinctValues_WhenTheColumnIsText — `ValuesAsync("Name")`: Ok with the 30 names
- test: Should_ReturnInvalid_WhenTheColumnIsNotReadableText — Theory "Quantity", "Secret", "Nope": Invalid, Reason names the column
- test: Should_ReturnTheRow_WhenGotById — Ok with the item; Should_ReturnNotFound_WhenTheIdIsMissing — `GetAsync(404)`: NotFound
- test: Should_CreateTheRow_WhenItHasNoId — `CreateAsync(new Item { Name = "new", Box = … })`: Created, Value.Id > 0, the row is in a new context
- test: Should_ReturnInvalid_WhenTheCreatedRowCarriesAnId — `CreateAsync(new Item { Id = 7 })`: Invalid, nothing written
- test: Should_SaveTheRowAndAnswerItAsStored_WhenUpdated — a detached `Memo` with the Id, a new Text and the version read from the database: Ok, the Text read in a new context is the new one, Value equals the row read in that context (Text and Version)
- test: Should_KeepTheReservedFields_WhenUpdated — a detached `Item` sent with a changed Name and a different `Secret` (`[JsonIgnore]`, not editable): Ok, the stored Name is the new one, the stored Secret is the seeded one
- test: Should_ReturnNotFound_WhenUpdatingAMissingRow — `UpdateAsync(new Memo { Id = 404, … })`: NotFound
- test: Should_ReturnConflict_WhenUpdatingAtAStaleVersion (PostgreSQL) — the version read, then the row changed through another context, then `UpdateAsync` with the old version: Conflict, Reason "The row was changed by another user.", the other change kept
- test: Should_DeleteAtTheVersionRead_WhenDeleted — `DeleteAsync(id, versionRead)`: Deleted, the row is gone
- test: Should_ReturnInvalid_WhenALockedRowIsDeletedWithoutAVersion — `DeleteAsync(id, null)` on a `Memo`: Invalid, Reason contains "?version=N" ; an `Item` (no row version) deletes with null: Deleted
- test: Should_ReturnInvalid_WhenTheVersionDoesNotFitTheRowVersion — `DeleteAsync(id, (ulong)uint.MaxValue + 1)` on a `Memo`: Invalid
- test: Should_ReturnNotFound_WhenDeletingAMissingRow — `DeleteAsync(404, versionOfAnother)`: NotFound
- test: Should_ReturnConflict_WhenDeletingAtAStaleVersion (PostgreSQL) — Conflict, the row still there
- test: Should_ReturnInvalid_WhenTheOperationThrowsInvalidRequest — `Probe.RunAsync` of an operation throwing `InvalidRequestException("bad")`: Invalid, Reason "bad"
- test: Should_ReturnConflict_WhenTheOperationThrowsStaleVersion — throwing `StaleVersionException("stale")`: Conflict, Reason "stale"
- test: Should_ReturnFaultedWithTheException_WhenTheOperationThrowsAnythingElse — throwing `InvalidOperationException("boom")`: Faulted, Exception the same instance
- test: Should_ReturnFaulted_WhenTheOperationIsCancelled — an operation that throws `OperationCanceledException` from a cancelled token: Faulted, Exception is `OperationCanceledException`
- test: Should_ReturnFaulted_WhenAConcurrencyExceptionHasNoToken — an `Item` (no token) updated through a tracked read and deleted by another context before the save, run through `Probe.RunAsync` with a plain `SaveChangesAsync`: Faulted with `DbUpdateConcurrencyException`
- test: Should_RecogniseTheLock_WhenTheFailedEntryHasAToken (OptimisticLockConflictTests, PostgreSQL) — the stale save of a `Memo`: `Is` true; Should_NotRecogniseTheLock_WhenNoEntryHasAToken — the missing `Item` update: `Is` false
- test: Should_ThrowArgumentNull_WhenTheEntityOrOptionsAreNull — `CreateAsync(null!)`, `UpdateAsync(null!)`, `LoadAsync(null!)`, `ValuesAsync(null!)`: ArgumentNullException before anything runs (misuse, not an outcome)
</red>

### Implementation

```csharp
// src/Zerox.Ef/Domain/EntityService.cs
using DevExtreme.AspNet.Data;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// The operations of an entity over its repository, for the API and for an in-process UI alike. Derive it for
/// an entity's own operations and run each through <see cref="RunAsync{R}"/>: an outcome is a status, never an
/// exception. Registered for every entity by AddZeroxDomain; a derived one by AddEntityService.
/// </summary>
public class EntityService<T>(IRepository<T> repository) : IEntityService<T> where T : class, IEntity
{
    private const string ChangedSince = "The row was changed by another user.";

    /// <summary>The repository of the entity, for a derived service's operations.</summary>
    protected IRepository<T> Repository { get; } = repository;

    /// <summary>The most rows one load reads; a service with larger pages overrides it.</summary>
    protected virtual int MaxTake => Search.DefaultMaxTake;

    /// <summary>The rows the load works on; a service with its own view of the rows overrides it.</summary>
    protected virtual IQueryable<T> SearchSource() => Repository.Query();

    public virtual Task<OpResult<Loaded>> LoadAsync(LoadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return RunAsync(async c => OpResult.Ok(LoadConversion.ToLoaded(
            await Search.LoadAsync(SearchSource(), LoadConversion.ToDevExtreme(options), MaxTake, c).ConfigureAwait(false))), ct);
    }

    public virtual Task<OpResult<IReadOnlyList<string>>> ValuesAsync(string column, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(column);
        return RunAsync(async c => DistinctTextValues.IsTextColumn<T>(column)
            ? OpResult.Ok(await DistinctTextValues.ValuesAsync(Repository.Query(), column, c).ConfigureAwait(false))
            : OpResult.Invalid<IReadOnlyList<string>>($"'{column}' is not a readable text column of {typeof(T).Name}."), ct);
    }

    public virtual Task<OpResult<T>> GetAsync(int id, CancellationToken ct = default) =>
        RunAsync(async c => await Repository.GetByIdAsync(id, c).ConfigureAwait(false) is { } entity
            ? OpResult.Ok(entity)
            : OpResult.NotFound<T>(), ct);

    public virtual Task<OpResult<T>> CreateAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RunAsync(async c =>
        {
            if (entity.Id != 0)
                return OpResult.Invalid<T>("The Id is given by the database: leave it out of a creation.");
            await Repository.AddAsync(entity, c).ConfigureAwait(false);
            await Repository.SaveChangesAsync(c).ConfigureAwait(false);
            return OpResult.Created(entity);
        }, ct);
    }

    public virtual Task<OpResult<T>> UpdateAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RunAsync(async c =>
        {
            Repository.Update(entity, EntityFields.Editable<T>());
            if (!await SaveUnlessMissingAsync(entity.Id, c).ConfigureAwait(false))
                return OpResult.NotFound<T>();
            return OpResult.Ok(await Repository.Query().SingleAsync(e => e.Id == entity.Id, c).ConfigureAwait(false));
        }, ct);
    }

    public virtual Task<OpResult<T>> DeleteAsync(int id, ulong? version, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            if (Repository.HasRowVersion && version is null)
                return OpResult.Invalid<T>($"{typeof(T).Name} has an optimistic lock: give the Version you read, as ?version=N.");
            if (!Repository.TryDeleteById(id, version))
                return OpResult.Invalid<T>($"{version} is not a version of {typeof(T).Name}: give the Version you read.");
            return await SaveUnlessMissingAsync(id, c).ConfigureAwait(false) ? OpResult.Deleted<T>() : OpResult.NotFound<T>();
        }, ct);

    /// <summary>
    /// Runs an operation and turns what it throws into its status: InvalidRequestException → Invalid,
    /// StaleVersionException or an optimistic-lock conflict → Conflict, anything else (cancellation included) →
    /// Faulted with the exception. The one place where an exception becomes an outcome.
    /// </summary>
    protected async Task<OpResult<R>> RunAsync<R>(Func<CancellationToken, Task<OpResult<R>>> operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        catch (InvalidRequestException e)
        {
            return OpResult.Invalid<R>(e.Message);
        }
        catch (StaleVersionException e)
        {
            return OpResult.Conflict<R>(e.Message);
        }
        catch (DbUpdateConcurrencyException e) when (OptimisticLockConflict.Is(e))
        {
            return OpResult.Conflict<R>(ChangedSince);
        }
        catch (Exception e)
        {
            return OpResult.Faulted<R>(e);
        }
    }

    /// <summary>
    /// Saves a caller's write. An UPDATE or DELETE that hit no row fails the same way whether the row is missing or
    /// changed since it was read; only then one read tells them apart: false when the row is missing, the conflict
    /// goes on (RunAsync makes it Conflict) when it exists.
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
}
```
(The generic parameter of `RunAsync` is named `R` only in this sketch; the code uses `TResult` — CA1715.)

`docs/catalog.md`, `## Zerox.Ef.Domain`: a row for `EntityService<T>` (the operations as results, derivable,
`RunAsync`, `SaveUnlessMissingAsync`, `MaxTake`, `SearchSource`) and one for `OptimisticLockConflict.Is`.

<success>
- [ ] `EntityService<T>` answers every operation as an `OpResult`, never throws for an outcome; a stale write is Conflict on PostgreSQL
- [ ] `OptimisticLockConflict.Is` exists in Domain; `docs/catalog.md` lists both rows
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` → all pass; `tools/test-postgres.sh --filter-class "*EntityServiceTests" --filter-class "*OptimisticLockConflictTests"` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="04-domain-registration">
## TDDAB-4: Register the entity services — every entity, and the derived ones

<intro>
Creates `src/Zerox.Ef/Domain/ServiceCollectionExtensions.cs` (`public static class ServiceCollectionExtensions`
in `Zerox.Ef.Domain`; package `Microsoft.Extensions.DependencyInjection.Abstractions` already reaches Domain
through `Zerox.Ef.Db`, which has its own `ServiceCollectionExtensions.AddZeroxDb`):
- `AddZeroxDomain(this IServiceCollection services)` — `TryAdd(ServiceDescriptor.Scoped(typeof(IEntityService<>), typeof(EntityService<>)))`:
  every entity has a service, scoped like the repository it uses;
- `AddEntityService<T, TContract, TService>(this IServiceCollection services)` where `T : class, IEntity`,
  `TContract : class, IEntityService<T>`, `TService : EntityService<T>, TContract` — `AddScoped<TService>()`,
  `AddScoped<TContract>(sp => sp.GetRequiredService<TService>())` and, when `TContract` is not
  `IEntityService<T>`, `AddScoped<IEntityService<T>>(sp => sp.GetRequiredService<TService>())`: one instance per
  scope behind both contracts, so `EntityController<T>` (which asks `IEntityService<T>`) gets the derived one.
Uses block 03's `EntityService<T>`. Tests: `tests/Zerox.Ef/Domain/ServiceRegistrationTests.cs` on
`new ServiceCollection().AddZeroxDb<TestDbContext>(o => db.Use(o))` (the `TestDb.Use` overload) built with
`new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }`, with test contracts
`IItemService : IEntityService<Item>` and `ItemService(IRepository<Item> r) : EntityService<Item>(r), IItemService`.
Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*ServiceRegistrationTests"`.
</intro>

<red>
- test: Should_ServeEveryEntity_WhenAddZeroxDomainIsCalled — `IEntityService<Item>` and `IEntityService<Memo>` resolve in a scope, as `EntityService<Item>` / `EntityService<Memo>`
- test: Should_GiveOneInstancePerScope — two resolutions in one scope are the same instance; two scopes give two
- test: Should_ServeTheDerivedService_UnderBothContracts — after `AddZeroxDomain().AddEntityService<Item, IItemService, ItemService>()`, `IItemService` and `IEntityService<Item>` resolve to the same `ItemService` instance in a scope
- test: Should_ServeTheDerivedService_WhenItHasNoContractOfItsOwn — `AddEntityService<Item, IEntityService<Item>, ItemService>()`: `IEntityService<Item>` is the `ItemService`, and exactly one registration of `IEntityService<Item>` exists
- test: Should_KeepTheDerivedService_WhenAddZeroxDomainIsCalledAfterIt — `AddEntityService<…>()` then `AddZeroxDomain()`: `IEntityService<Item>` is still the `ItemService`
- test: Should_PassTheValidation_WhenBuiltWithScopeChecks — building with `ValidateScopes` and `ValidateOnBuild` throws nothing
- test: Should_ThrowArgumentNull_WhenTheServicesAreNull — both methods with `null!`: ArgumentNullException
</red>

### Implementation

```csharp
// src/Zerox.Ef/Domain/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>The registration of the entity services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>An <see cref="IEntityService{T}"/> for every entity (scoped): <see cref="EntityService{T}"/>, unless a derived one was registered.</summary>
    public static IServiceCollection AddZeroxDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IEntityService<>), typeof(EntityService<>)));
        return services;
    }

    /// <summary>A derived service of <typeparamref name="T"/>, one instance per scope behind its own contract and <see cref="IEntityService{T}"/>.</summary>
    public static IServiceCollection AddEntityService<T, TContract, TService>(this IServiceCollection services)
        where T : class, IEntity
        where TContract : class, IEntityService<T>
        where TService : EntityService<T>, TContract
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<TService>();
        services.AddScoped<TContract>(sp => sp.GetRequiredService<TService>());
        if (typeof(TContract) != typeof(IEntityService<T>))
            services.AddScoped<IEntityService<T>>(sp => sp.GetRequiredService<TService>());
        return services;
    }
}
```
A closed `IEntityService<Item>` registration wins over the open generic whatever the order: the container
prefers the closed one (the test `Should_KeepTheDerivedService_WhenAddZeroxDomainIsCalledAfterIt` proves it).

`docs/catalog.md`, `## Zerox.Ef.Domain`: a row for `AddZeroxDomain` / `AddEntityService<T, TContract, TService>`;
`README.md` "Building on the database foundation": `Program.cs` calls `AddZeroxDomain()` after `AddZeroxDb`.

<success>
- [ ] Every entity resolves an `IEntityService<T>`; a derived service resolves under both contracts, one instance per scope
- [ ] `docs/catalog.md` and `README.md` show the registration
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` · `dotnet format zerox.csharp.core.slnx --verify-no-changes` · `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>
