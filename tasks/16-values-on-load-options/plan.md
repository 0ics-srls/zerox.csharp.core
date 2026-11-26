# TDDAB Plan: The distinct values follow the load — ValuesAsync on LoadOptions
**Date:** 2026-11-26

<mission>
## The project

**zerox.csharp.core** is the reusable C# core of 0ics. This work is in the **Zerox.Ef** family, the database →
API foundation of applications: `Zerox.Ef.Core` → `Zerox.Ef.Db` → `Zerox.Ef.Domain` → `Zerox.Ef.Api`. .NET 10
(SDK 10.0.400, C# 14), solution `zerox.csharp.core.slnx`, Central Package Management. Branch
`feature/16-values-on-load-options` (from `feature/15-hierid-parent-name`). Paths are relative to the repository
root. Every project sees its tests' internals. Everything in the repository is English.

```
src/Zerox.Ef/Core/     contracts, no dependency: IEntityService<T>, OpResult<T> + OpResult (Ok, Invalid, …), OpStatus,
                       LoadOptions (Filter, Group, Take, …), GroupingInfo (Selector, IsExpanded), Loaded (Data), LoadGroup (Key)
src/Zerox.Ef/Domain/   Search.LoadAsync(IQueryable<T> source, LoadOptions options, int maxTake, CancellationToken ct)
                       → Task<OpResult<Loaded>> (an option the query cannot answer is Invalid with the reason);
                       DistinctTextValues (IsTextColumn<T>, ValuesAsync); EntityService<T> (protected Repository,
                       MaxTake, SearchSource(), RunAsync; LoadAsync, ValuesAsync, …)
src/Zerox.Ef/Api/      ResultController (Answer), EntityController<T> (Load, Values, GetById, Create, Update, Delete)
tests/Zerox.Ef/Domain/ Zerox.Ef.Domain.Tests — TestDb.cs (Item: Name, Note, Quantity, Secret [JsonIgnore], Box;
                       TestDb.CreateAsync, NewContext(), NewScope()), DistinctTextValuesTests, EntityServiceTests
                       (30 items "item01".."item30" with Quantity 1..30; SmallOnes: SearchSource() = Quantity <= 3)
tests/Zerox.Ef/Api/    Zerox.Ef.Api.Tests — Sample/ (Customers: Acme/Rome, Bolt/Milan, Cargo/no city; 25 orders),
                       Sample/EntityControllerApiTests (SendAsync(method, url, json), PostAsync(url, json)),
                       Sample/OpenApiDocumentTests (keeps Sample/openapi/Sample.json equal to the served document),
                       Sample/client (TypeScript client generated from Sample.json, run by ClientTests)
```

## What this work changes, and why

`EntityService<T>.LoadAsync` runs `Search.LoadAsync(SearchSource(), options, MaxTake, ct)`.
`EntityService<T>.ValuesAsync(string column)` runs `DistinctTextValues.ValuesAsync(Repository.Query(), column, ct)`:
it takes no options, so the dropdown of a column's filter ignores the filter of the grid; and it skips
`SearchSource()`, so a service that narrows its rows (the caller's rows, a tenant's) answers the values of every
row. After this work the distinct values go through the same search as the load: on `SearchSource()`, with the
`Filter` of the `LoadOptions` the client sends, at most `MaxTake` values.

Of the options only `Filter` is read: a client sends the options of its grid as they are, and its sort, page,
select, group and summaries have no meaning for a list of distinct values. The contract becomes
`ValuesAsync(string column, LoadOptions options, CancellationToken ct)` and the endpoint
`POST api/<Entity>/values/{column}` with the options in the body; the `GET` is removed (breaking, written in
the README).

## Shared Capabilities

| Capability (what it does) | Owner (path + symbol) | Consumed by |
|---|---|---|
| Run load options on any query, in SQL, as an outcome | `src/Zerox.Ef/Domain/Search.cs`: `Search.LoadAsync(IQueryable<T>, LoadOptions, int, CancellationToken)` — EXISTS | `DistinctTextValues` |
| Is this name a readable text column | `src/Zerox.Ef/Domain/DistinctTextValues.cs`: `IsTextColumn<T>` — EXISTS | `DistinctTextValues`, `EntityService<T>` |
| The distinct values of a text column under a filter | `src/Zerox.Ef/Domain/DistinctTextValues.cs`: `ValuesAsync(source, column, filter, maxTake, ct)` — NEW | `EntityService<T>` |
| The rows and the limit of a service | `src/Zerox.Ef/Domain/EntityService.cs`: `SearchSource()`, `MaxTake` — EXISTS | `EntityService<T>.ValuesAsync` |
| An outcome translated to HTTP | `src/Zerox.Ef/Api/ResultController.cs`: `Answer` — EXISTS | `EntityController<T>.Values` |

## Design principles
- **KISS**: the values are the load with a fixed grouping; no second way to read a filter.
- **DRY / one implementation per capability**: one `ValuesAsync` in `DistinctTextValues`; the form without a
  filter is removed, not kept beside the new one. The filter is turned into SQL only by `Search.LoadAsync`.
- **The base is generic**: the fix is in `EntityService<T>`, so every derived service gets it by overriding
  `SearchSource()` alone.
- **SRP**: `DistinctTextValues` computes, `EntityService<T>` chooses the rows and the limit, the controller
  translates HTTP.
- **OCP**: a derived service changes the rows through `SearchSource()`; `ValuesAsync` is not edited for it.
- **LSP**: the outcomes stay the ones of the contract — `Ok` with the values, `Invalid` with the reason.
- **ISP**: `IEntityService<T>` keeps one member for the values, with the load's own options type.
- **DIP**: no use here — no new dependency is introduced.

## Code rules
- Everything in the repository is English — CLAUDE.md, Rules.
- One implementation per capability — CLAUDE.md, Rules; principles.md 2.1.
- An expected failure is a result (`OpResult`), misuse is an `ArgumentException` (`ArgumentNullException.ThrowIfNull`), no exception is caught to go on — CLAUDE.md, Rules.
- The public surface of `Zerox.Ef.*` carries XML comments (`///`): CS1591 is an error — CLAUDE.md, Rules.
- `internal sealed` by default; `public` is a decision — CLAUDE.md, Rules.
- Library code awaits with `ConfigureAwait(false)` (CA2007) — CLAUDE.md, Rules.
- Everything async, no synchronous road to the database; every I/O method takes a `CancellationToken` — CLAUDE.md, Rules; overlays/csharp/language.md §2.
- Every list is read in a bounded amount, computed by the database — branches/data.md 7.
- Every list operation runs on the server from the load options the client sends — branches/api.md 3.
- A published contract that breaks a consumer is said where the consumer reads it — branches/api.md 7.
- Tests: xunit v3 + AwesomeAssertions, names `Should_<Outcome>_When<Condition>`, one Act, `TestContext.Current.CancellationToken` on every async call — CLAUDE.md, Stack locks; testing-principles.md 1.4.
- The code that talks to the database is proved on the database, the transport through HTTP — testing-principles.md 3.2, 3.3.
- A change to the public surface updates `docs/catalog.md` in the same block — CLAUDE.md, Catalog.

## Commands
- Build: `dotnet build zerox.csharp.core.slnx -warnaserror`
- Format: `dotnet format zerox.csharp.core.slnx --verify-no-changes`
- ast-grep: `ast-grep scan`
- Fast tests: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"`
</mission>

<block id="01-values-under-a-filter">
## TDDAB-1: Compute the distinct values under a filter, through the search

<intro>
`src/Zerox.Ef/Domain/DistinctTextValues.cs` gets the computation the rest of the work uses: the distinct values
of a text column of ANY query, under a load-options filter, bounded, as an outcome. No dependency on other
blocks.

Capability: the distinct values of a column exist (`DistinctTextValues.ValuesAsync(IQueryable<T>, string,
CancellationToken)`, `Select → Where → Distinct → OrderBy`), but a load-options filter can be turned into SQL
only by `Search.LoadAsync` — which EXISTS and is used here, not rewritten: the new method asks the search for
the groups of the column and reads their keys. This block ADDS the new method beside the old one, so the
solution keeps building; block 02 removes the old one with its last caller.

File to change: `src/Zerox.Ef/Domain/DistinctTextValues.cs`. Tests:
`tests/Zerox.Ef/Domain/DistinctTextValuesTests.cs` (its seed: notes `pear`, `apple`, `pear`, null, empty, `fig`,
every item named `x`; the tests of this block add what they need to that seed — a `Quantity` per item, 1 to 6
in the order of the notes).

Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*DistinctTextValuesTests"`.
</intro>

<red>
- test: Should_ReturnDistinctOrderedValuesWithoutEmpty_WhenThereIsNoFilter — `apple`, `fig`, `pear`
- test: Should_ReturnOnlyTheValuesOfTheFilteredRows_WhenAFilterIsGiven — filter `["Quantity", "<=", 2]`: `apple`, `pear`
- test: Should_ReturnTheSameValuesAsTheLoadGroupedOnTheColumn_WhenAFilterIsGiven — for the same filter, the values equal the non-empty keys of `Search.LoadAsync` with `Group` on `Note`
- test: Should_ReturnNoValues_WhenTheFilterMatchesNoRow — `Ok` with an empty list
- test: Should_KeepTheViewOfTheQuery_WhenTheSourceIsNarrowed — source `Where(i => i.Quantity >= 5)`, no filter: `fig` only
- test: Should_ReturnAtMostMaxTakeValues_WhenThereAreMore — `maxTake` 2: two values, the first two in order
- test: Should_ReturnInvalid_WhenTheFilterNamesAnUnknownField — filter `["Nope", "=", 1]`: `Invalid`, the reason names `Nope`
- test: Should_ReturnInvalid_WhenTheFilterNamesAReservedField — filter `["Secret", "=", "s"]`: `Invalid`
- test: Should_NotLoadTheRows_WhenDistinctValuesAreAsked — one SQL command, which names neither `"Quantity"` nor `"Name"` among what it selects when no filter is given
- test: Should_ThrowArgumentNull_WhenTheSourceOrTheColumnIsNull
- test: Should_ThrowArgumentOutOfRange_WhenMaxTakeIsNotPositive
</red>

### Implementation

```csharp
// src/Zerox.Ef/Domain/DistinctTextValues.cs — added
/// <summary>
/// The distinct values of a text column of the rows the filter keeps: ordered, without repetitions and without
/// empty values, at most <paramref name="maxTake"/>, computed by the database. The filter is a load-options
/// filter and goes through the one search (<see cref="Search"/>), so a field it cannot name — unknown,
/// reserved — is <see cref="OpStatus.Invalid"/> with the reason, as in a load. The query comes from the
/// caller, so a view of its own (a subset of the rows) is kept.
/// </summary>
public static async Task<OpResult<IReadOnlyList<string>>> ValuesAsync<T>(
    IQueryable<T> source, string column, IReadOnlyList<object?>? filter, int maxTake, CancellationToken ct = default)
    where T : class
{
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(column);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTake);

    var options = new LoadOptions
    {
        Filter = filter,
        Group = [new GroupingInfo { Selector = column, IsExpanded = false }],
        Take = maxTake,
    };
    var loaded = await Search.LoadAsync(source, options, maxTake, ct).ConfigureAwait(false);
    if (loaded.Status != OpStatus.Ok || loaded.Value is null)
        return OpResult.Invalid<IReadOnlyList<string>>(loaded.Reason ?? "The values cannot be read.");

    // One group per distinct value, in the order of the key; the empty choice is left out: it would filter
    // on "" and empty the list without saying why.
    IReadOnlyList<string> values = [.. loaded.Value.Data.OfType<LoadGroup>()
        .Select(group => group.Key as string)
        .OfType<string>()
        .Where(value => value.Length > 0)];
    return OpResult.Ok(values);
}
```

`Search.LoadAsync` on `LoadOptions` answers `Ok` or `Invalid` only (any other failure it leaves to the caller as
an exception), so the branch above handles `Invalid`. The key of a group arrives as the column's value; when the
test shows it arrives in another form (a `JsonElement`), the reading of the key is fixed where it is read, in
this method, and nowhere else.

```csharp
// tests/Zerox.Ef/Domain/DistinctTextValuesTests.cs
private static readonly IReadOnlyList<object?> FirstTwo = ["Quantity", "<=", 2];
private const int Limit = 100;

[Fact]
public async Task Should_ReturnOnlyTheValuesOfTheFilteredRows_WhenAFilterIsGiven()
{
    await using var context = Db.NewContext();

    var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", FirstTwo, Limit, Ct);

    result.Status.Should().Be(OpStatus.Ok);
    result.Value.Should().Equal("apple", "pear");
}

[Fact]
public async Task Should_ReturnTheSameValuesAsTheLoadGroupedOnTheColumn_WhenAFilterIsGiven()
{
    await using var context = Db.NewContext();
    var grouped = await Search.LoadAsync(context.Items.AsNoTracking(),
        new LoadOptions { Filter = FirstTwo, Group = [new GroupingInfo { Selector = "Note", IsExpanded = false }], Take = Limit }, Limit, Ct);
    var keys = grouped.Value?.Data.OfType<LoadGroup>().Select(g => g.Key as string).OfType<string>().Where(k => k.Length > 0);

    var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", FirstTwo, Limit, Ct);

    result.Value.Should().Equal(keys);
}

[Fact]
public async Task Should_ReturnInvalid_WhenTheFilterNamesAnUnknownField()
{
    await using var context = Db.NewContext();

    var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", ["Nope", "=", 1], Limit, Ct);

    result.Status.Should().Be(OpStatus.Invalid);
    result.Reason.Should().Contain("Nope");
}
```

The existing tests of the old method stay green in this block: they are moved to the new one in block 02.

If a test or a criterion fails, the cause is found with Protocol D — `${CLAUDE_PLUGIN_ROOT}/commands/mind-sets/debug-protocol.md`, read in full — before any change.

<success>
- [ ] `DistinctTextValues` has the new `ValuesAsync` with its `///`, and turns no filter into a query itself: the only call that reads the filter is `Search.LoadAsync`
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` && `dotnet format zerox.csharp.core.slnx --verify-no-changes` && `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --stop-on-fail on` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="02-service-values-on-load-options">
## TDDAB-2: Make the service's values follow its search source and the load options

<intro>
Changes the contract and the service: `IEntityService<T>.ValuesAsync` takes the `LoadOptions`, and
`EntityService<T>.ValuesAsync` answers from `SearchSource()` under the options' `Filter`, at most `MaxTake`
values. Depends on block 01 (`DistinctTextValues.ValuesAsync(source, column, filter, maxTake, ct)`).

Capability: the rows and the limit of a service EXIST (`SearchSource()`, `MaxTake`), and so does the computation
(block 01): this block connects them and writes no query. It removes the old
`DistinctTextValues.ValuesAsync(IQueryable<T>, string, CancellationToken)`, whose last caller it replaces: one
implementation per capability.

The contract's change breaks its one caller, `EntityController<T>.Values`: this block adapts it to pass empty
options (`new LoadOptions()`), so the solution builds and the `GET` answers as before on the whole view; block
03 gives the endpoint its body.

Files to change:
- `src/Zerox.Ef/Core/IEntityService.cs` — the signature and its `///`
- `src/Zerox.Ef/Domain/EntityService.cs` — `ValuesAsync`
- `src/Zerox.Ef/Domain/DistinctTextValues.cs` — the old overload removed
- `src/Zerox.Ef/Api/EntityController.cs` — the call adapted (one line)
- `tests/Zerox.Ef/Domain/EntityServiceTests.cs`, `tests/Zerox.Ef/Domain/DistinctTextValuesTests.cs` (the tests of
  the old overload call the new one with a null filter)
- `docs/catalog.md` — the rows of `IEntityService<T>`, `DistinctTextValues` and `EntityService<T>`

Focused run: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --filter-class "*EntityServiceTests"`.
</intro>

<red>
- test: Should_ReturnTheDistinctValues_WhenTheColumnIsText — empty options: `Ok` with the 30 names
- test: Should_ReturnOnlyTheValuesOfTheFilteredRows_WhenTheOptionsHaveAFilter — `Filter` `["Quantity", "<=", 2]`: `item01`, `item02`
- test: Should_ReturnOnlyTheValuesOfTheSearchSource_WhenItIsOverridden — `SmallOnes` (`Quantity <= 3`), empty options: `item01`, `item02`, `item03`
- test: Should_ApplyTheFilterInsideTheSearchSource_WhenBothNarrow — `SmallOnes`, `Filter` `["Quantity", ">=", 3]`: `item03`
- test: Should_ReturnTheValuesOfTheRowsTheLoadReturns_WhenTheSameFilterIsGiven — for one filter, the values equal the distinct names of the rows `LoadAsync` answers
- test: Should_IgnoreThePageAndTheSortOfTheOptions_WhenValuesAreAsked — `Take` 1, `Skip` 5, `Sort` on `Quantity` descending: still the 30 names, in order
- test: Should_ReturnAtMostMaxTakeValues_WhenThereAreMore — a service whose `MaxTake` is 10: ten values
- test: Should_ReturnInvalid_WhenTheColumnIsNotReadableText (theory: `Quantity`, `Secret`, `Nope`) — the reason names the column
- test: Should_ReturnInvalid_WhenTheFilterNamesAReservedField — `Filter` `["Secret", "=", "s"]`
- test: Should_ThrowArgumentNull_WhenTheColumnOrTheOptionsAreNull
</red>

### Implementation

```csharp
// src/Zerox.Ef/Core/IEntityService.cs
/// <summary>
/// The distinct values of a text column, for the dropdown of its filter: of the rows the load works on, under
/// the <see cref="LoadOptions.Filter"/> of the options — the options of the client's grid as they are; their
/// sort, page, selection, grouping and summaries are not read. Ordered, without empty values, a bounded number.
/// Invalid when the column is not a readable text column, or the filter cannot be run.
/// </summary>
Task<OpResult<IReadOnlyList<string>>> ValuesAsync(string column, LoadOptions options, CancellationToken ct = default);
```

```csharp
// src/Zerox.Ef/Domain/EntityService.cs
/// <inheritdoc />
public virtual Task<OpResult<IReadOnlyList<string>>> ValuesAsync(string column, LoadOptions options, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(column);
    ArgumentNullException.ThrowIfNull(options);
    return RunAsync(c => DistinctTextValues.IsTextColumn<T>(column)
        ? DistinctTextValues.ValuesAsync(SearchSource(), column, options.Filter, MaxTake, c)
        : Task.FromResult(OpResult.Invalid<IReadOnlyList<string>>($"'{column}' is not a readable text column of {typeof(T).Name}.")), ct);
}
```

```csharp
// src/Zerox.Ef/Api/EntityController.cs — the one caller, adapted; its route and verb change in the next block
Answer(await Service.ValuesAsync(column, new LoadOptions(), ct).ConfigureAwait(false));
```

```csharp
// tests/Zerox.Ef/Domain/EntityServiceTests.cs
[Fact]
public async Task Should_ReturnOnlyTheValuesOfTheSearchSource_WhenItIsOverridden()
{
    await using var scope = Db.NewScope();
    var service = new SmallOnes(RepositoryOf<Item>(scope));

    var result = await service.ValuesAsync("Name", new LoadOptions(), Ct);

    result.Status.Should().Be(OpStatus.Ok);
    result.Value.Should().Equal("item01", "item02", "item03");
}

[Fact]
public async Task Should_IgnoreThePageAndTheSortOfTheOptions_WhenValuesAreAsked()
{
    await using var scope = Db.NewScope();
    var options = new LoadOptions { Take = 1, Skip = 5, Sort = [new SortingInfo { Selector = "Quantity", Desc = true }] };

    var result = await ServiceOf<Item>(scope).ValuesAsync("Name", options, Ct);

    result.Value.Should().Equal(Enumerable.Range(1, 30).Select(q => $"item{q:00}"));
}
```

`docs/catalog.md`: the row of `DistinctTextValues` says "under a load-options filter, through the search, at
most a limit, as a result"; the rows of `IEntityService<T>` and `EntityService<T>` say the values follow
`SearchSource()` and the options' filter.

If a test or a criterion fails, the cause is found with Protocol D — `${CLAUDE_PLUGIN_ROOT}/commands/mind-sets/debug-protocol.md`, read in full — before any change.

<success>
- [ ] `DistinctTextValues` has ONE `ValuesAsync`; nothing in `src/` reads `Repository.Query()` for the values
- [ ] `docs/catalog.md` describes the three types as they are after this block
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` && `dotnet format zerox.csharp.core.slnx --verify-no-changes` && `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Domain/Zerox.Ef.Domain.Tests.csproj --stop-on-fail on` · `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --stop-on-fail on` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

<block id="03-values-endpoint-with-options">
## TDDAB-3: Give the values endpoint the load options in its body

<intro>
The transport: `EntityController<T>.Values` becomes `POST api/<Entity>/values/{column}` with the `LoadOptions` in
the body, as `Load` has them. Depends on block 02 (`IEntityService<T>.ValuesAsync(column, options, ct)`).

Capability: the outcome's translation to HTTP EXISTS (`ResultController.Answer`), and so does the test that keeps
the contract (`OpenApiDocumentTests` rewrites `tests/Zerox.Ef/Api/Sample/openapi/Sample.json` and fails when the
served document changed): this block changes one action and reads the contract's diff.

The body may be left out: no options means no filter. The operation keeps its name, `<Entity>_Values`, so a
generated client keeps its method's name and gains the body. `GET api/<Entity>/values/{column}` no longer
exists (405): a breaking change, written in the README beside the one of `feature/11-entity-service`.

Files to change:
- `src/Zerox.Ef/Api/EntityController.cs` — the action and its `///`
- `tests/Zerox.Ef/Api/Sample/EntityControllerApiTests.cs` — the tests of the values
- `tests/Zerox.Ef/Api/Sample/OpenApiDocumentTests.cs` — one test on the values' operation
- `tests/Zerox.Ef/Api/Sample/openapi/Sample.json` — rewritten by the test; `tests/Zerox.Ef/Api/Sample/client/src/sample.test.ts` — one case that calls the generated `customersValues` with a filter
- `README.md` (step 5: `POST api/Customers/values/{column}` with the load options; a **Breaking
  (feature/16-values-on-load-options)** paragraph: the contract's new parameter, the `GET` gone, a derived
  service that overrode `ValuesAsync` only to narrow its rows can drop the override), `docs/catalog.md` (the row
  of `EntityController<T>`)

Focused run: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --filter-method "*Values*"`.
</intro>

<red>
- test: Should_ReturnDistinctCities_WhenValuesIsCalledWithoutABody — `POST api/Customers/values/City`: 200, `Milan`, `Rome`
- test: Should_ReturnOnlyTheCitiesOfTheFilteredCustomers_WhenValuesIsCalledWithAFilter — body `{"Filter":["Name","=","Acme"]}`: `Rome`
- test: Should_ReturnDistinctCities_WhenValuesIsCalledWithTheOptionsOfAGrid — body `{"Take":1,"Skip":1,"Sort":[{"Selector":"Name","Desc":true}],"Select":["Name"]}`: still `Milan`, `Rome`
- test: Should_Answer400_WhenValuesNamesAColumnThatIsNotText (theory: `Version`, `Nope`) — `Code` `InvalidRequest`
- test: Should_Answer400_WhenValuesNamesAReservedField — `POST api/Orders/values/InternalNote`; the detail does not carry a stored value
- test: Should_Answer400WithTheReason_WhenTheFilterOfValuesNamesAnUnknownField — `POST api/Customers/values/City`, body `{"Filter":["Nope","=",1]}`: 400, `Code` `InvalidRequest`, the detail names `Nope`
- test: Should_Answer405_WhenValuesIsAskedWithGet — `GET api/Customers/values/City`: 405 as a ProblemDetails
- test: Should_DocumentTheValuesWithTheLoadOptionsAsItsBody_WhenTheDocumentIsServed — `/api/Customers/values/{column}` has `post` and no `get`, operation `Customers_Values`, body `LoadOptions` or null, answer an array of strings
- test: Should_KeepTheFileEqualToTheServedDocument — the existing test: `Sample.json` rewritten once, its diff read, then green
- test: the generated client reads the cities of the customers named Acme (`sample.test.ts`, run by `ClientTests`)
</red>

### Implementation

```csharp
// src/Zerox.Ef/Api/EntityController.cs
/// <summary>
/// The distinct values of a text column, for the dropdown of its filter. POST because the options travel in the
/// body, as in the load; the meaning is a read. Of the options only the Filter is read — a client sends the
/// options of its grid as they are — and the body may be left out. The column name comes from the URL and is
/// checked against the real properties before any query.
/// </summary>
[HttpPost("values/{column}")]
public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, [FromBody] LoadOptions? options, CancellationToken ct) =>
    Answer(await Service.ValuesAsync(column, options ?? new LoadOptions(), ct).ConfigureAwait(false));
```

```csharp
// tests/Zerox.Ef/Api/Sample/EntityControllerApiTests.cs
[Fact]
public async Task Should_ReturnOnlyTheCitiesOfTheFilteredCustomers_WhenValuesIsCalledWithAFilter()
{
    var (status, body) = await PostAsync("api/Customers/values/City", """{"Filter":["Name","=","Acme"]}""");

    status.Should().Be(HttpStatusCode.OK);
    body.EnumerateArray().Select(v => v.GetString()).Should().Equal("Rome");
}

[Fact]
public async Task Should_Answer405_WhenValuesIsAskedWithGet()
{
    var (status, _) = await SendAsync(HttpMethod.Get, "api/Customers/values/City");

    status.Should().Be(HttpStatusCode.MethodNotAllowed);
}
```

A reserved field named in the filter is proved at the service's level (block 02): the sample has no entity with
both a text column and a reserved field, so the endpoint proves the refusal of a filter with an unknown field.

If a test or a criterion fails, the cause is found with Protocol D — `${CLAUDE_PLUGIN_ROOT}/commands/mind-sets/debug-protocol.md`, read in full — before any change.

<success>
- [ ] `git diff tests/Zerox.Ef/Api/Sample/openapi/Sample.json` shows, for `Customers` and `Orders`, the values' operation moved from `get` to `post` with the `LoadOptions` body, and nothing else
- [ ] `README.md` has the breaking paragraph and `docs/catalog.md` the controller's row as it is after this block
- [ ] Gate: `dotnet build zerox.csharp.core.slnx -warnaserror` && `dotnet format zerox.csharp.core.slnx --verify-no-changes` && `ast-grep scan` → exit 0
- [ ] Block tests: `dotnet test --project tests/Zerox.Ef/Api/Zerox.Ef.Api.Tests.csproj --stop-on-fail on` → all pass
- [ ] Regression: `dotnet test --solution zerox.csharp.core.slnx -- --filter-not-trait "Category=Integration"` → all pass
</success>
</block>

## Execution Order
01-values-under-a-filter             → no dependencies
02-service-values-on-load-options    → depends on 01
03-values-endpoint-with-options      → depends on 02
