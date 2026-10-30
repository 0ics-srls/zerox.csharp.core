# 10 — The optimistic lock on the database's row version

**Date:** 2026-10-30 · **Branch:** `feature/10-db-row-version` (from `feature/09-single-load`)

## The request

The user, reviewing CcV3 (the first consumer of Zerox.Ef): the optimistic lock of Zerox.Ef was built
wrong. "Optimistic lock can work only with a row version made by the database." "We will not set this
version or anything: it is EF's job to manage it." "No triggers, nothing: use the abstraction EF has, which
picks the right method on the database layer; with SQLite the user is warned that there is no optimistic
lock." And on the flow: "I load the record with rowVersion = 100; in the save call EF sees that the
database does not have that value and refuses it. No pin, nothing."

## What is wrong today

1. **`Version` is incremented by the application** (`SessionInterceptor`: 0 on an added row, `++` on a
   modified one). Every write that does not go through `SaveChanges` leaves it unchanged — the library's
   own `MassiveOpHandler.UpdateWhereAsync` (`ExecuteUpdate`), raw SQL, another process.
   **Proven on PostgreSQL (CcV3, 2026-10-30):** client A reads the row at Version 0; a write outside the
   library changes `Description` and Version stays 0; A's PUT with Version 0 answers 200 and silently
   overwrites that write. A lost update, with nobody told.
2. **The PUT reloads the row and copies the body onto it** (`EntityController.Update`: `GetByIdAsync` +
   `EntityFields.CopyEditable`). Reloaded, the row's original version is the database's, not the client's;
   `PinReadVersion` puts the client's back by hand. A patch on a design error. `LastWriterWins` does the
   same for a client version.

## The design

- **The row version is EF's**: an entity declares `[Timestamp]` (EF's `IsRowVersion()`: a concurrency
  token the database generates on add and update) on a property of the type its provider generates —
  `uint` on PostgreSQL (Npgsql maps it to the system column `xmin`), `ulong` / `long` on SQL Server
  (`rowversion`). Zerox.Ef never sets it, never increments it.
- **A client's row is saved as the client sent it**: attached, only its editable fields marked modified;
  EF puts the version the client read in the `WHERE`, the database refuses a row changed since
  (`DbUpdateConcurrencyException` → 409). No reload, no pin. A DELETE attaches a stub `{ Id, version }`.
- **404 vs 409**: an UPDATE or DELETE of a missing row fails exactly like a stale one (0 rows). Only when
  the save fails, one `Any(Id)` read tells them apart: missing → 404, present → 409.
- **What the client cannot send is never written**: the key, what the database generates, a field with a
  private setter or `[JsonIgnore]` are not marked modified. The PUT answers the row as stored (one read
  after the save), never the body.
- **SQLite has no row version**: `ApplyOptimisticLock(Database)` gives every row version the default 0
  (else the INSERT fails: NOT NULL with no value) and logs one warning through the application's logger:
  the lock is off, every save goes through.
- **A client version is a number**: `?version=N` binds a `ulong`, converted to the row version's type.
  A row version that is not `uint`/`ulong`/`long` is refused when the model is built (`byte[]` included:
  on SQL Server use `ulong`); on PostgreSQL only `uint` (`xmin`).
- **An action that needs the current row** (e.g. a consumer's state machine trigger) reads it, compares
  its version with the client's first and throws `StaleVersionException` → 409, before deciding anything.
- **`LastWriterWins` keeps only the server's own changes** (retry on conflict, last writer wins); a
  client's single-field change is a client row with that one field (`Update(entity, [field])`).

## Facts verified before planning (spike, EF Core 10.0.13, Npgsql 10.0.3, Sqlite 10.0.13)

| Scenario | PostgreSQL | SQLite |
|---|---|---|
| `[Timestamp] uint Version` in the model | column `xmin`, type `xid`, OnAddOrUpdate, token | column `Version` INTEGER, OnAddOrUpdate, token |
| INSERT | ✓ version read back (e.g. 1331) | ✗ DbUpdateException (no value) — ✓ with `HasDefaultValue(0u)`: 0 |
| tracked update | ✓ version changes (1331 → 1332) | with default: stays 0 |
| attach + mark `Name` modified, stale version | ✗ DbUpdateConcurrencyException | with default: saved (no lock) |
| attach, current version | ✓, not marked fields kept, version read back | ✓ |
| attach marking `Version` modified too | ✓ ignored by EF (no error) | ✓ |
| `ExecuteUpdate` | ✓ version changes | stays 0 |
| attach update of a version read before `ExecuteUpdate` | ✗ DbUpdateConcurrencyException | saved |
| attach update of a missing row | ✗ DbUpdateConcurrencyException | ✗ DbUpdateConcurrencyException |
| `Remove(stub)` stale / current | ✗ concurrency / ✓ deleted | deleted (no lock) |
| `Database.GetService<ILoggerFactory>()` in `OnModelCreating` | — | ✓ reaches the app's logger, once per model |

Sources: EF Core "Handling Concurrency Conflicts", "Generated Values", `PropertyBuilder.IsRowVersion`
("Database providers can choose to interpret this in different way"); Npgsql "Concurrency tokens" (`uint`
→ `xmin`, 7.0+); EF PR #29961 (`long`/`ulong` rowversion on SQL Server); EF docs: SQLite has no
automatically-updating type.

## Code Organization

| Capability | Owner |
|---|---|
| Row version in the model, SQLite default + warning, refused types | `Zerox.Ef.Db` — `ModelBuilderExtensions.ApplyOptimisticLock(ModelBuilder, DatabaseFacade)` |
| Save / delete a client's row at the version it read; whether an entity has a row version | `Zerox.Ef.Core.IRepository<T>` (`Update(T, fields)`, `TryDeleteById(id, version)`, `HasRowVersion`), implemented in `Zerox.Ef.Db.Repository<T>` |
| The fields a client may write | `Zerox.Ef.Domain.EntityFields.Editable<T>()` |
| A stale client version found by a read | `Zerox.Ef.Domain.StaleVersionException`, answered 409 by `Zerox.Ef.Api.ErrorHandlingMiddleware` |
| 404 vs 409 after a failed save | `Zerox.Ef.Api.EntityController<T>.SaveUnlessMissingAsync` |

Removed: `IHasOptimisticLock`, `IRepository<T>.PinReadVersion`, the Version code of `SessionInterceptor`,
`EntityFields.CopyEditable`, `LastWriterWins`' client version.

## Inventory — capabilities and where they already live

- Optimistic concurrency, generated version, read back after save → **EF Core + provider** (`IsRowVersion`
  / `[Timestamp]`, Npgsql `xmin`): used, never re-implemented.
- 409 for a concurrency conflict with a token → `ErrorHandlingMiddleware.IsOptimisticLockConflict`: kept.
- The editable fields (public setter, not the key, not a navigation, not reserved) → the selection inside
  `EntityFields` (`Editable<T>` private class): reused by `Editable<T>()`.
- Reserved field / navigation tests → `EntityPaths.IsReserved`, `EntityPaths.IsNavigation`: reused.
- Massive update/delete → `MassiveOpHandler`: unchanged (the database now versions its writes).
- Log capture for tests → `LogCapture` in `tests/Zerox.Ef/Api/TestHost.cs`: moved to a shared linked file.
- Test database (Sqlite / PostgreSQL) → `tests/Zerox.Ef/TestDatabase.cs` (`OnPostgres`): reused.

## Out of scope

CcV3 (the consumer) is adapted by its own plan after this one: `TaskItem.Version` → `[Timestamp] uint`,
a migration dropping its `Version` column, the trigger action comparing versions first.
