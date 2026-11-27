# Changelog

All notable changes to this project are written here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). A release is a git tag `v<major>.<minor>.<patch>` on `main`;
every assembly carries the version `1.0.<n>` of `version.build`.

## [1.0.0] - 2026-11-27

The first release. The library is consumed as a git submodule pinned at this tag (see the README).

### Zerox.Core
- Concurrency: the owner queue (`OwnerQueue`, whose `DisposeAsync` returns once the job in flight has ended), the
  critical section that holds across `await`, the atomic boolean, the cancellation tree on `TimeProvider`.
- Scheduling: jobs, the job queue ring, the job scheduler (a suspended job does not run), and the working process that
  runs the cycles (it ends when the stop cancels the cycle in flight).
- Collections, numbers and statistics: the sorted search on spans, the bit mask of arbitrary length, the circular linked
  list, the circular buffer with interval views, the typed number interval on generic math, the numeric, row and moving
  window series, the series aggregator and its groups, the declarative series of services.
- Identity: the hierarchical id as an immutable value (`HierId`), which tells its name and its parent.
- Text, reflection, validation, editing and messaging: the text helpers and the one JSON setup, the one value
  conversion, the reflection helpers and the compiled property accessor, the validation rules and the one validator,
  the deep clone and the comparison of POCOs, the edit copy, the point-to-point message channel, the one change
  notification, the basic remote data provider.

### Zerox.Core.Avalonia
- The property container and its throttle pump, the command and its information, the async command and the observable
  task, the minimal dialog service, and `UiThread`, which runs work on the UI thread.

### Zerox.Dnf
- The boolean rule compiler: expression → disjunctive normal form → masks.

### Zerox.Ef (Core, Db, Domain, Api)
- The database foundation on EF Core, asynchronous only: `IDbContext`, repositories, the session rules on save, the
  boundary (`IUnitOfWork`), the exclusive massive operation (inside the provider's execution strategy), the optimistic
  lock on the row version the database generates (a client's row is saved and deleted at the version it read; SQLite
  has none and is warned about), last writer wins for single-field actions.
- `EntityService<T>`: every entity operation, outcomes as results; the entity services registered for every entity and
  the derived ones.
- The search over an `IQueryable` on the library's own `LoadOptions` (converted to DevExtreme's and back): rows, groups,
  projection, the load options validator; `Search.LoadAsync` on any query, as a result; the distinct values under a
  filter, through the search.
- ASP.NET Core: `EntityController<T>` on the entity service (the transport never touches the storage),
  `ResultController` (the translation of an outcome for any controller), one error format (ProblemDetails with the code
  of the domain, the framework's own failures included), JSON as is, the OpenAPI document a client is generated from.
- Refused instead of passing in silence: a `null` in the load options or on a required day is "not set"; an open
  boundary in a request, an enum written as a number, a fractional group interval.

### Breaking, for a project that followed the feature branches
- `EntityController<T>` extends `ResultController`.
- `values/{column}` is `POST` with the load options in its body (was `GET`), and answers `TextValue` items (was
  `string`).
