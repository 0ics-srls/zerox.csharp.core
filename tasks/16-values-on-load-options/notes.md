# 16-values-on-load-options — the distinct values follow the load

## Requirements

©User, 2026-11-26, from the project `carta-modelli` (which builds on this library as a submodule):
*"EntityService<T>.ValuesAsync deve accettare lo stesso dataloadoptions che accetta GetAll o come si chiama"* —
`ValuesAsync` must accept the same load options the load accepts. Decided with ©User the same day:

- one way only: `ValuesAsync(column, LoadOptions)` and `POST values/{column}` with the options in the body, like
  `load`; the `GET` goes away (a breaking change for a consumer, written in the README);
- on a new branch, `feature/16-values-on-load-options` from `feature/15-hierid-parent-name`, so a consumer of the
  earlier branch keeps what it has; ©User brings the branches to `main` when the line is finished.

## Analysis

What the code does today (`src/Zerox.Ef/Domain/EntityService.cs`):

```csharp
LoadAsync(LoadOptions options)  →  Search.LoadAsync(SearchSource(), options, MaxTake, c)
ValuesAsync(string column)      →  DistinctTextValues.ValuesAsync(Repository.Query(), column, c)
```

Two defects in `ValuesAsync`:

1. **It takes no options.** The dropdown of a column's filter lists the values of the whole table even when the
   grid is already filtered on other columns.
2. **It skips `SearchSource()`.** A derived service that narrows the rows (`SearchSource()` overridden — the rows
   of the caller, of a tenant) still answers the distinct values of every row: what the load hides, the values
   show. `tests/Zerox.Ef/Domain/EntityServiceTests.cs` proves the narrowing for the load
   (`Should_LoadOnlyTheSearchSource_WhenItIsOverridden`) and has no such test for the values.

The controller has the same shape: `GET api/<Entity>/values/{column}` (`src/Zerox.Ef/Api/EntityController.cs`)
has no body to carry a filter.

`DistinctTextValues.ValuesAsync(IQueryable<T>, string, CancellationToken)` computes with
`Select → Where → Distinct → OrderBy` on the query it is given. A filter of the load options is a DevExtreme
filter (a JSON array): only DevExtreme's loader turns it into SQL, and it gives no filtered `IQueryable` back.
So the filter can reach the values only through the one search, `Search.LoadAsync(IQueryable<T>, LoadOptions,
maxTake, ct)`: grouped on the column, its answer is one `LoadGroup` per distinct value, computed in SQL, with the
load's own validation (unknown field, reserved field, wrong operator → `Invalid` with the reason).

Mindsets: `principles.md`, `code-principles.md`, `testing-principles.md`; branches `service-layers`, `data`,
`api`; overlay `csharp` (`language.md`, `testing.md`, `tddab.md`, `efcore.md`, `aspnetcore.md`).

## Inventory — capabilities and where they already live

| Capability | Where it lives | Decision |
|---|---|---|
| Run load options (filter, group) on any query, in SQL, as an outcome | `src/Zerox.Ef/Domain/Search.cs`: `Search.LoadAsync(IQueryable<T>, LoadOptions, int, CancellationToken)` | reuse |
| The rows a service works on | `src/Zerox.Ef/Domain/EntityService.cs`: `SearchSource()`, `MaxTake` | reuse |
| Is this name a readable text column | `src/Zerox.Ef/Domain/DistinctTextValues.cs`: `IsTextColumn<T>` | reuse |
| The distinct values of a column | `src/Zerox.Ef/Domain/DistinctTextValues.cs`: `ValuesAsync` | extend: it takes the filter and goes through the search; the form without a filter is removed (one way) |
| An outcome translated to HTTP | `src/Zerox.Ef/Api/ResultController.cs`: `Answer` | reuse |
| The contract a client is generated from, kept by a test | `tests/Zerox.Ef/Api/Sample/OpenApiDocumentTests.cs`, `openapi/Sample.json` | reuse |

## Code Organization

- **Contract** (`Zerox.Ef.Core`, `IEntityService<T>`):
  `Task<OpResult<IReadOnlyList<TextValue>>> ValuesAsync(string column, LoadOptions options, CancellationToken ct = default)`.
- **What of the options counts**: the `Filter`, and nothing else. A client sends the options of its grid as they
  are; sort, page, select, group and summaries have no meaning for a list of distinct values and are not read
  (the grid's `Take` of 10 must not cut a dropdown to 10 values).
- **Bound**: at most `MaxTake` values (`data` 7: every list is bounded), ordered, without the empty ones.
- **The answer** (©User 2026-11-26: *"con groups è forse anche meglio perché abbiamo anche la cardinalità"*,
  then *"mi piace 1"*): each value with the number of rows that have it — `TextValue(string Value, int Count)`, a
  new record in `Zerox.Ef.Core`. The grouping gives the count in the same query; the contract breaks once, now.
- **Owner of the computation**: `DistinctTextValues.ValuesAsync(IQueryable<T> source, string column,
  IReadOnlyList<object?>? filter, int maxTake, CancellationToken ct)`, an `OpResult`, through `Search.LoadAsync`
  grouped on the column. `EntityService<T>.ValuesAsync` gives it `SearchSource()` and `MaxTake`.
- **Transport**: `POST api/<Entity>/values/{column}`, body `LoadOptions` (may be left out), operation
  `<Entity>_Values` as before.
- **Mechanism**: none of its own — it is the load's pipeline with a fixed grouping.
- **Not in this work**: a unique-key violation still leaves `RunAsync` as `Faulted` and not `Conflict`
  (noticed in `carta-modelli`; a work of its own, when a consumer needs it).

## TDDAB Rules Applied

1. Bottom-up, one layer a block: the computation in `Domain`, then the contract and the service, then the
   transport; each block's tests run at its layer's level (the query on the test database, the service through
   its contract, the endpoint through HTTP).
2. RED is the contract: the service's tests call the service; the endpoint's status codes belong to the
   endpoint's block.
3. What exists is used: the blocks call `Search.LoadAsync`, `SearchSource()`, `IsTextColumn<T>` and `Answer`, and
   the RED pins the values to what the load itself answers for the same filter.
4. Every block leaves the solution building and green: the block that changes the contract adapts its one caller.
5. No open decision, no phase in the plan: commits and the Memory Bank are the executor's.
