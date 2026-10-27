# Zerox.Domain — the DevExtreme search, the validator, last writer wins

<block id="05-domain-validator">
## TDDAB-5: Create Zerox.Domain with the Load Options Validator and the Safe Options

<intro>
Creates `src/Zerox.Domain` (references `src/Zerox.Db`, package `DevExtreme.AspNet.Data` 5.1.0 added to
`Directory.Packages.props`) and `tests/Zerox.Domain.Tests` (references `src/Zerox.Domain`, package
`Microsoft.EntityFrameworkCore.Sqlite`); add both to `zerox.csharp.core.slnx`.

Delivers:
- `InvalidRequestException` (public sealed, ctor `(string)`, `(string, Exception)`): a request that
  cannot be executed, with the reason written for whoever built it (the middleware of block 08 turns it
  into a 400 with the message);
- `LoadOptionsValidator.Validate<T>(DataSourceLoadOptionsBase options)` (public static): checks the
  whole options object and at the first error throws `InvalidRequestException` with the reason AND the
  right form. Why it exists (measured on DevExtreme 5.1.0): for a value that does not convert DevExtreme
  does not throw, it returns 0 rows silently; a 4-element condition and an `"xor"` return ALL rows; a
  `Sort` without `Selector` is ignored — an empty or full answer taken as good is the worst damage;
- `Search.MakeSafe(DataSourceLoadOptionsBase options)` (public static, in `Search.cs`, the rest of
  `Search` arrives in block 06): negative `Skip`/`Take` → `InvalidRequestException`; forces
  `AllowAsyncOverSync = false`, `RemoteGrouping = null`, `RemoteSelect = null` (no sync code on the
  database, grouping and projection done by the provider in SQL); every other option is left as is.

Port of the validator: the same rules, the same lists (operators `=`, `<>`, `<`, `>`, `<=`, `>=`,
`contains`, `notcontains`, `startswith`, `endswith`; summaries `sum`, `min`, `max`, `avg`, `count`;
group intervals `year`, `quarter`, `month`, `day`, `dayOfWeek`, `hour`, `minute`, `second`), messages in
English with neutral examples (`["Quantity", ">", 2]`, `Box.Label`). Fields resolve case-insensitively
segment by segment through navigations; a collection is not entered and is allowed at the end only in
`Select`/`PreSelect`; the list of fields in an "unknown field" message never contains reserved fields
(`[JsonIgnore]` that is not a navigation to an `IEntity`).

Source: `<filo>/src/Filo.Server/Controllers/ValidatoreRichiesta.cs` (whole file),
`<filo>/src/Filo.Server/Controllers/RicercaDevExtreme.cs` (`OpzioniSicure`, `RisolviPercorso`,
`TipoDentro`, `EUnaNavigazione` — the last three become `internal static` helpers in
`src/Zerox.Domain/EntityPaths.cs`), `<filo>/src/Filo.Core/Exceptions/RichiestaNonValidaException.cs`.
Tests: `<filo>/tests/Filo.Server.Tests/Controllers/ValidatoreRichiestaTests.cs`,
`<filo>/tests/Filo.Server.Tests/Batteria/OpzioniSicureTests.cs`.

Test entities: `tests/Zerox.Domain.Tests/TestDb.cs` with `Box : IEntity` (`Id`, `Label` string,
`Opened` `DateOnly`, `List<Item> Items`), `Item : IEntity` (`Id`, `Name` string, `Quantity` int,
`Price` decimal, `Kind` enum `ItemKind {Small, Large}`, `At` `DateTime`, `Note` string? , `Secret`
string with `[JsonIgnore]`, `BoxId`, `Box`), `TestDbContext : DbContext, IDbContext`, and the same
`TestDb` helper as block 01 (one Sqlite in-memory connection, `UseZeroxSession()`).

Files: `src/Zerox.Domain/Zerox.Domain.csproj`, `InvalidRequestException.cs`, `LoadOptionsValidator.cs`,
`EntityPaths.cs`, `Search.cs`; `tests/Zerox.Domain.Tests/Zerox.Domain.Tests.csproj`, `TestDb.cs`,
`LoadOptionsValidatorTests.cs`.
</intro>

<red>
- test: Should_NameTypeOptionAndFields_WhenAFilterFieldDoesNotExist — message contains the type `Item`, the option `Filter` and the available fields
- test: Should_ListTheFieldsOfTheNavigatedType_WhenASegmentAfterANavigationDoesNotExist — `Box.Nope` lists the fields of `Box`
- test: Should_NotListReservedFields_WhenListingAvailableFields — `Secret` is never in the list
- test: Should_NameTheOption_WhenAFieldDoesNotExistInSortGroupSummarySelectPreSelectDefaultSortPrimaryKey — one case per option
- test: Should_Pass_WhenFieldsAndNavigationsUseAnyCase — `quantity`, `BOX.label`
- test: Should_Throw_WhenSortGroupOrSummaryHasNoSelector_ButCountPasses — a `count` summary without selector is valid
- test: Should_NotEnterACollection_AndAllowItAtTheEndOnlyInSelect — `Items.Name` throws; `Items` in `Select` passes, in `Sort` throws
- test: Should_ListTheTenOperators_WhenTheOperatorIsUnknown_ButAnyCasePasses — `"like"` throws with the ten; `"CONTAINS"` passes
- test: Should_Throw_WhenATextOperatorIsUsedOnANonTextField
- test: Should_ShowTheRightForm_WhenAConditionHasTheWrongArity
- test: Should_Throw_WhenTheFieldIsNotAString — `[1, "=", 2]`
- test: Should_Throw_WhenTheSeparatorIsUnknown_ButImplicitAndPasses — `"xor"` throws; two conditions side by side pass
- test: Should_Throw_WhenNegationHasNoCondition
- test: Should_Throw_WhenTheValueHasTheWrongType — `"abc"` on `Quantity`, `"x"` on `At`, `"Medium"` on `Kind`; each message shows the right form
- test: Should_Throw_WhenAListIsGivenInsteadOfAValue
- test: Should_Pass_WhenTheValuesAreRight — int, decimal, ISO date on `DateTime` and on `DateOnly`, enum name, null on a nullable
- test: Should_Throw_WhenNullIsGivenToANonNullableField
- test: Should_Throw_WhenTheSummaryIsUnknownOrOnTheWrongType — `"median"`; `sum` on a string
- test: Should_AllowGroupIntervalsOnlyOnTheRightType — `month` on a date passes, on an int throws; `hour` on a `DateOnly` throws
- test: Should_Pass_WhenTheRequestIsRight — a full request (filter, sort, group, summaries, select)
- test: Should_Pass_WhenFilterIsNullOrEmptyAndOptionsAreEmpty
- test: Should_ForceAllowAsyncOverSyncFalse_WhenMadeSafe — even if `true` came in
- test: Should_NullRemoteGroupingAndRemoteSelect_WhenMadeSafe
- test: Should_Throw_WhenSkipOrTakeIsNegative
- test: Should_LeaveTheOtherOptionsAsTheyAre_WhenMadeSafe — `Skip`, `Take`, `Filter`, `Sort`, `RequireTotalCount` unchanged
</red>

### Implementation

```csharp
namespace Zerox.Domain;

public sealed class InvalidRequestException : Exception
{
    public InvalidRequestException() : base("Invalid request.") { }
    public InvalidRequestException(string message) : base(message) { }
    public InvalidRequestException(string message, Exception innerException) : base(message, innerException) { }
}

public static class LoadOptionsValidator
{
    public static IReadOnlyList<string> Operators { get; } =
        ["=", "<>", "<", ">", "<=", ">=", "contains", "notcontains", "startswith", "endswith"];
    public static IReadOnlyList<string> Summaries { get; } = ["sum", "min", "max", "avg", "count"];
    public static IReadOnlyList<string> Intervals { get; } =
        ["year", "quarter", "month", "day", "dayOfWeek", "hour", "minute", "second"];

    public static void Validate<T>(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var type = typeof(T);
        if (options.Filter is { Count: > 0 } filter) Filter(type, filter);
        foreach (var s in options.Sort ?? []) Field(type, s?.Selector, "Sort");
        foreach (var g in options.Group ?? []) Group(type, g);
        foreach (var r in options.TotalSummary ?? []) Summary(type, r, "TotalSummary");
        foreach (var r in options.GroupSummary ?? []) Summary(type, r, "GroupSummary");
        foreach (var p in (options.Select ?? []).Where(p => p != "this")) Field(type, p, "Select", collectionAtEnd: true);
        foreach (var p in (options.PreSelect ?? []).Where(p => p != "this")) Field(type, p, "PreSelect", collectionAtEnd: true);
        if (options.DefaultSort is { } sort) Field(type, sort, "DefaultSort");
        foreach (var k in options.PrimaryKey ?? []) Field(type, k, "PrimaryKey");
    }

    // Field, NotFound, Filter, Condition, Value, WrongValue, Summary, Group, Underlying, IsCollection,
    // IsNumericType, IsNumber, IsInteger, Describe: port of the Filo methods of the same role,
    // one for one, English messages.
}

internal static class EntityPaths
{
    internal const BindingFlags PublicProperties = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    // The chain of properties of "Box.Label", case-insensitive like DevExtreme; null if a segment is missing.
    internal static IReadOnlyList<PropertyInfo>? Resolve(Type type, string path) { /* port of RisolviPercorso */ }

    internal static bool IsNavigation(Type type) => typeof(IEntity).IsAssignableFrom(Inner(type));

    // The type a path goes on with: the element for a collection, else the type itself (string is not a collection).
    internal static Type Inner(Type type) { /* port of TipoDentro */ }

    internal static bool IsReserved(PropertyInfo property) =>
        property.IsDefined(typeof(JsonIgnoreAttribute)) && !IsNavigation(property.PropertyType);
}

public static class Search   // completed in block 06, same file
{
    public static void MakeSafe(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Skip < 0 || options.Take < 0)
            throw new InvalidRequestException("Skip and Take cannot be negative.");
        options.AllowAsyncOverSync = false;
        options.RemoteGrouping = null;
        options.RemoteSelect = null;
    }
}
```

`Search` is ONE file, `src/Zerox.Domain/Search.cs`; block 06 adds its methods there.

<success>
- [ ] All RED tests of `LoadOptionsValidatorTests` pass
- [ ] `src/Zerox.Domain`, `tests/Zerox.Domain.Tests` in the solution; versions only in `Directory.Packages.props`
- [ ] No Filo name, Filo field (`RagioneSociale`, `Ore`, `Cliente`…) or Italian in `src/Zerox.Domain`
- [ ] `docs/catalog.md` has a `Zerox.Domain` section; `README.md` lists `Zerox.Domain`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="06-domain-search">
## TDDAB-6: Add the Search (Rows, Groups, Projection) and the Distinct Text Values

<intro>
Completes `src/Zerox.Domain/Search.cs` (block 05 created it with `MakeSafe`) and adds
`DistinctTextValues` and `GroupsResult`. Uses `LoadOptionsValidator`, `EntityPaths`,
`InvalidRequestException` (block 05) — no second validation, no second path resolver.

- `Search.RowsAsync<T>(IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct)`
  → `DataSourceResult<T>`: `MakeSafe`; reserved fields check; `Group` present → `InvalidRequestException`
  ("grouping is asked on the groups route"); `Select` or `PreSelect` present →
  `InvalidRequestException` ("projection is asked on the projection route"); `Validate<T>`; then
  `DataSourceLoader.LoadAsync`, materialized HERE (`data = loaded.data?.Cast<T>().ToList() ?? []`) so an
  error leaves this method and not the serializer; `ArgumentException`, `InvalidOperationException`,
  `FormatException`, `NotSupportedException` from DevExtreme become `InvalidRequestException` with the
  original as inner (the ONE rethrow-as-other in the library: a malformed request is not a crash).
- `Search.GroupsAsync<T>(...)` → `GroupsResult`: `Group` required; `Select` refused; every level
  `IsExpanded = false` (keys, counts, sums — never rows); maps DevExtreme `Group` to `GroupNode`
  recursively; negative counts become null.
- `Search.ProjectionAsync<T>(...)` → `DataSourceResult<IDictionary<string, object?>>`: `Select` or
  `PreSelect` required; `Group` refused; selected paths rewritten to the C# names
  (`"box.label"` → `"Box.Label"`) so keys are "as is".
- Reserved fields: every path named anywhere in the options (filter fields, sort, group, summaries,
  select, preselect, primary key, default sort) is resolved; a `[JsonIgnore]` property that is not a
  navigation → `InvalidRequestException("The field 'Secret' cannot be read here.")`. A route with load
  options is full access to the table: this is what keeps a secret out of `Select` and `Group`.
- `DistinctTextValues.IsTextColumn<T>(string column)`: EXACT name (case-sensitive: names are the
  contract), `string` type, not `[NotMapped]`. `DistinctTextValues.ValuesAsync<T>(IQueryable<T>, string
  column, CancellationToken)`: `EF.Property<string>`, no null, no empty, distinct, ordered — in SQL.
- `DataSourceResult<T>` (public sealed): the shape DevExtreme clients read — properties `data`
  (`IEnumerable<T>`, default empty), `totalCount` (int, default -1), `groupCount` (int, default -1),
  `summary` (`object[]?`), each with `[JsonPropertyName]` of the same lowercase name and
  `#pragma warning disable IDE1006` with the comment "lowercase: the DevExtreme contract, the one exception
  to names as is" (port of `<filo>/src/Filo.Core/Models/DataSourceResult.cs`; it is NOT a DevExtreme type);
- `GroupsResult(IReadOnlyList<GroupNode> Groups, int? GroupCount, int? TotalCount, object?[]? Summary)`,
  `GroupNode(object? Key, int? Count, object?[]? Summary, IReadOnlyList<GroupNode>? Children)`.

Source: `<filo>/src/Filo.Server/Controllers/RicercaDevExtreme.cs`, `ColonneDiTesto.cs`,
`<filo>/src/Filo.Core/Models/RisultatoGruppi.cs`. Tests:
`<filo>/tests/Filo.Server.Tests/Controllers/BaseControllerValoriDistintiTests.cs` (domain half), plus the
new ones below.

Files: `src/Zerox.Domain/Search.cs`, `DataSourceResult.cs`, `GroupsResult.cs`, `DistinctTextValues.cs`;
`tests/Zerox.Domain.Tests/SearchTests.cs`, `DistinctTextValuesTests.cs`.
</intro>

<red>
- test: Should_ReturnTheDevExtremeShape_WhenSerialized — a `DataSourceResult<Item>` serialized with names as is has exactly the keys `data`, `totalCount`, `groupCount`, `summary`
- test: Should_PageFilterAndSort_WhenRowsAreAsked — 30 items seeded: `Skip 10 Take 5` sorted by `Quantity` desc with a filter returns the right 5 and `totalCount` of the filtered set
- test: Should_ReturnRowsAsEntities_WhenRowsAreAsked — `data` is a `List<Item>`
- test: Should_ReturnCountAndSummaryWithoutRows_WhenOnlyCountOrSummaryIsAsked — `IsCountQuery`/`IsSummaryQuery`: `data` empty, not null
- test: Should_PointToTheGroupsRoute_WhenGroupIsAskedForRows
- test: Should_PointToTheProjectionRoute_WhenSelectOrPreSelectIsAskedForRows
- test: Should_TurnDevExtremeErrorsIntoInvalidRequest — an operator DevExtreme rejects reaches the caller as `InvalidRequestException` with the inner exception
- test: Should_RefuseAReservedField_InFilterSortGroupSummaryAndSelect — `Secret` in each
- test: Should_AllowANavigation_EvenWhenItIsJsonIgnored — a `[JsonIgnore]` navigation to an entity is readable through its fields
- test: Should_ReturnKeysCountsAndSums_WhenGroupsAreAsked — group by `Kind` with a `sum` of `Quantity`: two `GroupNode`, right counts and sums, no rows
- test: Should_ReturnChildren_WhenGroupingOnTwoLevels — `Kind` then `Box.Label`
- test: Should_GroupByMonth_WhenAnIntervalIsGiven — `At` grouped by `month`
- test: Should_RequireGroup_WhenGroupsAreAsked — and `Select` is refused
- test: Should_ReturnDictionariesWithCSharpNames_WhenProjectionIsAsked — `Select ["name", "box.label"]` → keys `Name` and `Box.Label`
- test: Should_RequireSelect_WhenProjectionIsAsked — and `Group` is refused
- test: Should_ReturnDistinctOrderedValuesWithoutEmpty — duplicates once, alphabetical, null and "" out
- test: Should_RecognizeOnlyExactMappedTextColumns — `Name` yes; `name` no; `Quantity` no; a `[NotMapped]` string no; a missing name no
- test: Should_NotLoadTheRows_WhenDistinctValuesAreAsked — a `DbCommandInterceptor` added to the test context captures the command text: it contains `DISTINCT` and no row of the table is materialized
</red>

### Implementation

```csharp
namespace Zerox.Domain;

public static class Search
{
    public static void MakeSafe(DataSourceLoadOptionsBase options) { /* block 05 */ }

    public static async Task<DataSourceResult<T>> RowsAsync<T>(
        IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        MakeSafe(options);
        CheckReserved<T>(options);
        if (options.Group is { Length: > 0 })
            throw new InvalidRequestException("Grouping is not asked here: use the groups route. Here rows are read.");
        // PreSelect alone projects too (measured on 5.1.0): it would return dictionaries, not entities.
        if (options.Select is { Length: > 0 } || options.PreSelect is { Length: > 0 })
            throw new InvalidRequestException("Projection is not asked here: use the projection route. Here whole entities are read.");
        LoadOptionsValidator.Validate<T>(options);
        try
        {
            var loaded = await DataSourceLoader.LoadAsync(source, options, ct).ConfigureAwait(false);
            return new DataSourceResult<T>
            {
                data = loaded.data?.Cast<T>().ToList() ?? [],
                totalCount = loaded.totalCount,
                groupCount = loaded.groupCount,
                summary = loaded.summary,
            };
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            throw new InvalidRequestException($"Invalid search: {e.Message}", e);
        }
    }

    public static Task<GroupsResult> GroupsAsync<T>(IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default) { /* see intro */ }

    public static Task<DataSourceResult<IDictionary<string, object?>>> ProjectionAsync<T>(IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default) { /* see intro */ }

    // Every path named in the options, resolved segment by segment; a reserved property throws.
    private static void CheckReserved<T>(DataSourceLoadOptionsBase options) { /* port of ControllaPercorsi + PercorsiDi + CampiDelFiltro + NonRiservato */ }
}

#pragma warning disable IDE1006 // lowercase: the DevExtreme contract, the one exception to names as is
public sealed class DataSourceResult<T>
{
    [JsonPropertyName("data")] public IEnumerable<T> data { get; set; } = [];
    [JsonPropertyName("totalCount")] public int totalCount { get; set; } = -1;
    [JsonPropertyName("groupCount")] public int groupCount { get; set; } = -1;
    [JsonPropertyName("summary")] public object[]? summary { get; set; }
}
#pragma warning restore IDE1006

public sealed record GroupsResult(IReadOnlyList<GroupNode> Groups, int? GroupCount, int? TotalCount, object?[]? Summary);

public sealed record GroupNode(object? Key, int? Count, object?[]? Summary, IReadOnlyList<GroupNode>? Children);

public static class DistinctTextValues
{
    public static bool IsTextColumn<T>(string column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var property = typeof(T).GetProperty(column, BindingFlags.Public | BindingFlags.Instance);
        return property is not null && property.PropertyType == typeof(string)
            && property.GetCustomAttribute<NotMappedAttribute>() is null;
    }

    public static async Task<IReadOnlyList<string>> ValuesAsync<T>(IQueryable<T> query, string column, CancellationToken ct = default)
        where T : class =>
        await query.Select(row => EF.Property<string>(row, column))
            .Where(value => value != null && value != "")
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(ct).ConfigureAwait(false);
}
```

<success>
- [ ] All RED tests of `SearchTests` and `DistinctTextValuesTests` pass; block 05 tests still pass
- [ ] `Search`, `GroupsResult`, `DistinctTextValues` in `docs/catalog.md`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="07-domain-last-writer">
## TDDAB-7: Add LastWriterWins for Single-Field Actions

<intro>
Delivers `LastWriterWins` (public sealed, in `src/Zerox.Domain`), the helper for an action that changes
ONE field and must win even if somebody saved in between (in Filo it was a method of a controller base;
here it is a service so any controller or service uses it).

`LastWriterWins(IRepositoryFactory repositories)`; `const int MaxAttempts = 3`;
`Task<T?> SaveAsync<T>(int id, Expression<Func<T, bool>> scope, Func<T, CancellationToken, Task> apply,
int? versionReadByClient = null, CancellationToken ct = default) where T : class, IEntity`:
- each attempt reads the row TRACKED (`Query(tracked: true).Where(scope)` with `Id == id`); missing or
  outside the scope → returns null at once, no retry (the caller answers 404);
- only on the FIRST attempt, if `versionReadByClient` is given and `T` is `IHasOptimisticLock`,
  `PinReadVersion` (so a stale client is detected);
- `apply`, then `SaveChangesAsync`; on `DbUpdateConcurrencyException` with attempts left: `DetachAll()`
  and loop (from the second attempt the fresh version is used: that is how the last writer wins);
  on the last attempt the exception reaches the caller (the middleware of block 08 answers 409).
- No explicit transaction, no lock: only repeated saves.

Uses `IRepositoryFactory`, `IRepository<T>.PinReadVersion`, `DetachAll` (blocks 01, 04).

Source: `<filo>/src/Cartellino.Server/Controllers/CartellinoControllerBase.cs` (`SaveWithLastWriterWinsAsync`,
`MaxConcurrencyRetries`). Tests: `<filo>/tests/Cartellino.Server.Tests/Controllers/OptimisticLockRetryTests.cs`.
Test entity: `tests/Zerox.Domain.Tests/TestDb.cs` gains `Memo : IEntity, IHasOptimisticLock` (`Id`,
`Owner` string, `Text` string, `Version`), and `TestDbContext` calls `ApplyOptimisticLock()`. A
conflict is produced by a second context that saves the same row from inside `apply` (first attempt
only).

Files: `src/Zerox.Domain/LastWriterWins.cs`; `tests/Zerox.Domain.Tests/TestDb.cs`, `LastWriterWinsTests.cs`.
</intro>

<red>
- test: Should_RunOnce_WhenThereIsNoConflict — `apply` called once, value saved, `Version` 1
- test: Should_ReReadAndSave_WhenAConflictHappens — `apply` called twice, the save succeeds
- test: Should_KeepTheLastWritersValues_AfterTheRetry — the field set by `apply` wins; a field changed only by the other writer is kept
- test: Should_HaveTheVersionIncrementedByBothWriters_AfterTheRetry — `Version` 2
- test: Should_Throw_WhenAttemptsAreExhausted — a conflict on every attempt: `DbUpdateConcurrencyException` after `MaxAttempts` calls of `apply`
- test: Should_ReturnNullWithoutRetry_WhenTheRowDisappears — deleted by the other writer: null, `apply` not called again
- test: Should_DetectAStaleClient_WhenTheClientVersionIsGiven — `versionReadByClient: 0` while the row is at 1: the first save conflicts, the retry re-reads and wins
- test: Should_NotOpenATransaction — `context.Database.CurrentTransaction` is null inside `apply`
- test: Should_NotTouchARowOutsideTheScope — scope `Owner == "a"` on a `Memo`, row of `"b"`: null, row unchanged
</red>

### Implementation

```csharp
namespace Zerox.Domain;

// For an action that changes ONE field: re-read and re-apply on conflict, so the last writer wins.
// No lock is ever taken: only repeated saves.
public sealed class LastWriterWins(IRepositoryFactory repositories)
{
    public const int MaxAttempts = 3;

    public async Task<T?> SaveAsync<T>(
        int id,
        Expression<Func<T, bool>> scope,
        Func<T, CancellationToken, Task> apply,
        int? versionReadByClient = null,
        CancellationToken ct = default) where T : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(apply);
        var repository = repositories.For<T>();
        for (var attempt = 0; ; attempt++)
        {
            var entity = await repository.Query(tracked: true).Where(scope)
                .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
            if (entity is null) return null;

            if (attempt == 0 && versionReadByClient is int read && entity is IHasOptimisticLock)
                repository.PinReadVersion(entity, read);

            await apply(entity, ct).ConfigureAwait(false);
            try
            {
                await repository.SaveChangesAsync(ct).ConfigureAwait(false);
                return entity;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts - 1)
            {
                repository.DetachAll();   // the next attempt reads the database, not the stale copy
            }
        }
    }
}
```

`LastWriterWins` is not registered by `AddZeroxDb` (it is Domain): a project registers it
(`services.AddScoped<LastWriterWins>()`), as the sample does in block 10.

<success>
- [ ] All RED tests of `LastWriterWinsTests` pass; blocks 05–06 tests still pass
- [ ] `LastWriterWins` in `docs/catalog.md`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
