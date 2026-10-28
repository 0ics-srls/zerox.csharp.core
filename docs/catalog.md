# Catalog

What the library offers, by need. Before writing a capability, look for it here; then read the type
with LSAI (`lsai_search <Type>`, `lsai_outline <file>`, `lsai_source <Type>`, `lsai_usages <Type>`).
Paths are relative to `src/`.

## Zerox.Core

### Concurrency and cancellation

| Need | Type | File |
|---|---|---|
| Run jobs one at a time, each to its end, the caller gets its own job's result (owner's queue) | `OwnerQueue` (`RunAsync`, `StartDetached`) | `Zerox.Core/Concurrency/OwnerQueue.cs` |
| Start work outside the caller's flow; an escaping exception stops the process | `OwnerQueue.StartDetached` | `Zerox.Core/Concurrency/OwnerQueue.cs` |
| An async lock with timeout, released by `using` (acquired only with await: `LockAsync`, `TryLockAsync`) | `CriticalSection`, `CriticalSectionLock` | `Zerox.Core/Concurrency/CriticalSection.cs` |
| A boolean switched atomically (false→true once) | `AtomicBoolean` (`FalseToTrue`, `TrueToFalse`, `SetWhen`) | `Zerox.Core/Concurrency/AtomicBoolean.cs` |
| A cancellation tree for the application, scopes with their own timeout | `Terminator`, `CancellationScope`, `CancellationState` | `Zerox.Core/Cancellation/` |

### Scheduling

| Need | Type | File |
|---|---|---|
| Many cyclic jobs sharing one cycle: main jobs, one slow job per main lap, one priority job per tick | `JobScheduler` (`TickAsync`, `Create*Job` with async work, `Errors`) | `Zerox.Core/Scheduling/JobScheduler.cs` |
| Jobs that run once, or once after a due time | `Job.Once`, `DelayedJob`, `JobScheduler.Create*OneTime*Job` | `Zerox.Core/Scheduling/` |
| Suspend and resume a job or a group of jobs | `JobScheduler.SuspendJob/ResumeJob/SuspendGroup/ResumeGroup` | `Zerox.Core/Scheduling/JobScheduler.cs` |
| A failing job reported without stopping the others | `JobScheduler.Errors` (channel of `JobError`) | `Zerox.Core/Scheduling/JobError.cs` |
| A ring of jobs, one per call, itself a job | `JobQueue` | `Zerox.Core/Scheduling/JobQueue.cs` |
| Your own job | `IJob`, `JobBase`, `JobKind`, `JobState` | `Zerox.Core/Scheduling/` |
| A periodic loop on a `TimeProvider` running an async cycle (first cycle at once, each cycle awaited, stop awaited, cycle statistics) | `WorkingProcess` | `Zerox.Core/Scheduling/WorkingProcess.cs` |

### Collections and search

| Need | Type | File |
|---|---|---|
| A fixed-capacity ring buffer with front/back push and pop, view by interval | `CircularBuffer<T>` | `Zerox.Core/Collections/CircularBuffer.cs` |
| A circular list walked one element per `Next()` | `CircularLinkedList<T>` | `Zerox.Core/Collections/CircularLinkedList.cs` |
| Lower/upper bound, first/last binary search on a sorted span | `SortedSearch` | `Zerox.Core/Collections/SortedSearch.cs` |
| A range of keys: from–to, since, last N, last time span | `Interval<T>`, `Interval` | `Zerox.Core/Collections/Interval.cs` |
| A bit mask of any size with and/or | `BitMask` | `Zerox.Core/Collections/BitMask.cs` |
| A comparer of key/value pairs by key | `KeyComparer.For` | `Zerox.Core/Collections/KeyComparer.cs` |

### Statistics (realtime series)

| Need | Type | File |
|---|---|---|
| A series of values by time or index: min, max, last, sum, mean, rate, view by interval | `DataSeries<TKey,TValue>` | `Zerox.Core/Statistics/DataSeries.cs` |
| A series of rows (several channels per key) | `ArraySeries<TKey,TValue>` | `Zerox.Core/Statistics/ArraySeries.cs` |
| A moving window of the last N values with its mean and the mean's extremes | `CyclicSeries<TValue>` | `Zerox.Core/Statistics/CyclicSeries.cs` |
| Keys of a series: time from a `TimeProvider`, or an index | `TimeKeys`, `IndexKeys`, `ISeriesKeys<T>` | `Zerox.Core/Statistics/SeriesKeys.cs` |
| Hardware counters that wrap (16/32 bit) widened to 64 bit | `CounterWidening` | `Zerox.Core/Statistics/CounterWidening.cs` |
| One registry of every series, by name, by hierarchical id, by group | `SeriesAggregator`, `SeriesGroup` | `Zerox.Core/Statistics/` |
| Series created and sampled from the `[Aggregable]` properties of a service | `AggregableSources`, `AggregableAttribute`, `IAggregableSource` | `Zerox.Core/Statistics/` |
| New points of chosen series sent periodically through a channel | `RemoteDataProvider`, `SeriesUpdate` | `Zerox.Core/Statistics/RemoteDataProvider.cs` |

### Objects, editing, validation

| Need | Type | File |
|---|---|---|
| `INotifyPropertyChanged` with `SetProperty` | `NotifyingObject` | `Zerox.Core/ComponentModel/NotifyingObject.cs` |
| An edit copy of a plain object: saved state, reset, changed or not, JSON in/out on a stream | `PocoProxy<T>` | `Zerox.Core/Editing/PocoProxy.cs` |
| Deep clone by fields, compare two objects member by member | `PocoExtensions` (`CloneByFields`, `IsChanged`), `MemberDifference` | `Zerox.Core/Editing/PocoExtensions.cs` |
| Rules per property, errors for bindings (`INotifyDataErrorInfo`), validatable children | `Validator<T>`, `ValidationRules<T>`, `ValidationRule<T>`, `IValidatable`, `ValidationParameters`, `ValidationException` | `Zerox.Core/Validation/` |

### Messaging and identity

| Need | Type | File |
|---|---|---|
| One message between a source and a destination held weakly | `MessageChannel<TKind>`, `Message<TKind>`, `IMessageSource<TKind>`, `IMessageDestination<TKind>` | `Zerox.Core/Messaging/` |
| A hierarchical id (`a.b.c`): prefix, common root, pattern match | `HierId` | `Zerox.Core/Identity/HierId.cs` |

### Numbers, text, reflection

| Need | Type | File |
|---|---|---|
| A numeric range for any number type: clamp, contains, saturating add/multiply, parse within range | `NumberRange<T>`, `NumberRange` | `Zerox.Core/Numbers/NumberRange.cs` |
| JSON with the library's options (System.Text.Json), on a stream | `JsonDefaults` (`Options`, `ToJsonAsync`, `FromJsonAsync`) | `Zerox.Core/Text/JsonDefaults.cs` |
| Truncate, clip, Base64 UTF-8 | `TextExtensions` | `Zerox.Core/Text/TextExtensions.cs` |
| Convert a value to a type (enums, nullable, convertible) | `ValueConversion.ConvertTo` | `Zerox.Core/Text/ValueConversion.cs` |
| Properties marked by an attribute, an attribute of a property | `AttributeExtensions` | `Zerox.Core/Reflection/AttributeExtensions.cs` |
| Fast get/set of a property through compiled delegates | `FastProperty<TInstance,TValue>`, `FastProperty.For` | `Zerox.Core/Reflection/FastProperty.cs` |

## Zerox.Core.Avalonia

| Need | Type | File |
|---|---|---|
| A view model base: `PropertyChanged` on the UI thread, optional throttle and debounce, `MemberChanged` at once | `PropertyContainer` | `Zerox.Core.Avalonia/ComponentModel/PropertyContainer.cs` |
| An async command: one run at a time, cancellable, timeout, progress on the UI thread | `AsyncCommand<T>`, `AsyncCommand.Create`, `AsyncCommand.WithProgress` | `Zerox.Core.Avalonia/Commands/AsyncCommand.cs` |
| What menus and toolbars see of a command | `ICommandWithInfo` | `Zerox.Core.Avalonia/Commands/ICommandWithInfo.cs` |
| A task the bindings can follow (running, failed, done) | `ObservableTask` | `Zerox.Core.Avalonia/Commands/ObservableTask.cs` |
| Notice, warning, error, question boxes | `IDialogService`, `DialogService`, `DialogServiceExtensions` (`NotifyAsync`, `ConfirmAsync`, `ShowErrorAsync`), `MessageKind` | `Zerox.Core.Avalonia/Dialogs/` |

## Zerox.Dnf

| Need | Type | File |
|---|---|---|
| Compile a boolean rule (`C1.F2 & !C3.F1`) to masks and evaluate it on a 64-bit input | `RuleCompiler`, `CompiledRule`, `IRuleCompiler` | `Zerox.Dnf/RuleCompiler.cs` |
| Parse an expression to a tree, build one by code | `BoolExprParser`, `BoolExpr` (+ `ConstExpr`, `BitRefExpr`, `NotExpr`, `AndExpr`, `OrExpr`), `ExpressionNode` | `Zerox.Dnf/` |
| Disjunctive normal form, minimized | `DnfConverter`, `DnfResult`, `DnfTerm` | `Zerox.Dnf/DnfConverter.cs` |
| Rules to bytes and back | `RuleSerializer`, `IRuleSerializer` | `Zerox.Dnf/RuleCompiler.cs` |

## Zerox.Ef.Core

The contracts of the database foundation, no EF type in them: what an application's own layers reference.

| Need | Type | File |
|---|---|---|
| An entity with its own int key | `IEntity` | `Zerox.Ef/Core/IEntity.cs` |
| An opt-in optimistic lock: `Version` as concurrency token, 0 for an added row, incremented on every save of a modified row | `IHasOptimisticLock` (+ `IRepository<T>.PinReadVersion`, `DetachAll`) | `Zerox.Ef/Core/IHasOptimisticLock.cs` |
| The port to a table: read (not tracked by default), unsaved rows, get (tracked: change it and save), add, delete, save — no update of a whole detached entity | `IRepository<T>` | `Zerox.Ef/Core/IRepository.cs` |
| The repository of a type decided at run time, on the same context | `IRepositoryFactory` (`For<T>`) | `Zerox.Ef/Core/IRepositoryFactory.cs` |
| One write for an operation over several repositories: saves inside are swallowed, `ApplyAsync` writes once (nested = slave) | `IUnitOfWork` (from `IRepository<T>.OpenUnitOfWork()`) | `Zerox.Ef/Core/IUnitOfWork.cs` |
| Update or delete many rows without loading them, exclusive, in its own transaction (no commit = rollback) | `IMassiveOpHandler<T>` (from `IRepository<T>.OpenMassiveAsync()`), `IFieldsToSet<T>` | `Zerox.Ef/Core/IMassiveOpHandler.cs`, `Zerox.Ef/Core/IFieldsToSet.cs` |

## Zerox.Ef.Db

| Need | Type | File |
|---|---|---|
| The context the library works on (a project writes `MyContext : DbContext, IDbContext`, nothing else) | `IDbContext` | `Zerox.Ef/Db/IDbContext.cs` |
| Register a context with the session rules, `IRepository<>` and `IRepositoryFactory`; the end of the scope throws if a unit of work or a massive operation was left open (closed first) | `ServiceCollectionExtensions.AddZeroxDb<TContext>` | `Zerox.Ef/Db/ServiceCollectionExtensions.cs` |
| Build context options by hand with the session rules (every synchronous road to the database refused) | `DbContextOptionsBuilderExtensions.UseZeroxSession` | `Zerox.Ef/Db/DbContextOptionsBuilderExtensions.cs` |
| The error of a synchronous road to the database (a query read with `ToList`, `SaveChanges`, a command, a transaction or a connection opened without await) | `SyncDatabaseAccessException` | `Zerox.Ef/Db/SyncDatabaseAccessException.cs` |
| Mark `Version` as concurrency token on every `IHasOptimisticLock` entity (end of `OnModelCreating`) | `ModelBuilderExtensions.ApplyOptimisticLock` | `Zerox.Ef/Db/ModelBuilderExtensions.cs` |

## Zerox.Ef.Domain

| Need | Type | File |
|---|---|---|
| A request that cannot be executed, with the reason for whoever built it (the API answers 400) | `InvalidRequestException` | `Zerox.Ef/Domain/InvalidRequestException.cs` |
| Check DevExtreme load options before running them: unknown field, operator, value type, interval → the reason and the right form | `LoadOptionsValidator.Validate<T>` | `Zerox.Ef/Domain/LoadOptionsValidator.cs` |
| The options a client does not decide (no sync over async, grouping and projection in SQL, no negative paging) | `Search.MakeSafe` | `Zerox.Ef/Domain/Search.cs` |
| One search from DevExtreme load options, the options deciding the shape: rows (filter, sort, paging — Take required, at most `DefaultMaxTake` or the caller's limit; a count or a summary alone needs none — total, summaries), only the asked columns (`Select`: names through navigations, keys as the C# names), or keys, counts and sums per group (`Group`: never rows, nested levels, date intervals) — server-side, DevExtreme's own `LoadResult` as is, errors as 400 | `Search.LoadAsync` | `Zerox.Ef/Domain/Search.cs` |
| A `[JsonIgnore]` field never readable through load options | `Search` (reserved fields check) | `Zerox.Ef/Domain/Search.cs` |
| The distinct values of a text column for a filter dropdown, in SQL (never a reserved field) | `DistinctTextValues` (`IsTextColumn<T>`, `ValuesAsync`) | `Zerox.Ef/Domain/DistinctTextValues.cs` |
| Apply an entity sent whole onto the stored row: every editable field, never the key, the navigations or the reserved fields | `EntityFields.CopyEditable` | `Zerox.Ef/Domain/EntityFields.cs` |
| A single-field action: with the client's Version the optimistic lock (stale = conflict, never retried); without it re-read and re-apply on conflict, the last writer wins | `LastWriterWins` (`SaveAsync<T>`, `MaxAttempts`) | `Zerox.Ef/Domain/LastWriterWins.cs` |

## Zerox.Ef.Api

| Need | Type | File |
|---|---|---|
| The controller of an entity: one DevExtreme load (`POST load`: rows, columns or groups), distinct values, CRUD (POST without an Id, 400 with one; a locked row starts at Version 0; PUT copies the editable fields onto the stored row, 404 when it is missing; PUT and DELETE `?version=N` at the Version the client read, stale = 409) — no `[Authorize]` (the project adds it); a project writes one line per entity | `EntityController<T>` | `Zerox.Ef/Api/EntityController.cs` |
| The top of every request: every error as one JSON body with a code — 400 invalid request (with the reason), 409 optimistic-lock conflict, 500 otherwise (logged with its stack); a client gone is no error; a started response is aborted; the process never dies of a request | `ApplicationBuilderExtensions.UseZeroxErrorHandling`, `ApiError`, `ApiErrorCode` | `Zerox.Ef/Api/ApplicationBuilderExtensions.cs`, `ApiError.cs`, `ErrorHandlingMiddleware.cs` |
| JSON "as is" (no camelCase), enums as names, days as yyyy-MM-dd — controllers, minimal APIs, OpenAPI alike | `ZeroxJson.AddZeroxJson`, `ZeroxJson.AddConverters` | `Zerox.Ef/Api/ZeroxJson.cs` |
| A day as yyyy-MM-dd, nullable too | `DateOnlyJsonConverter`, `NullableDateOnlyJsonConverter` | `Zerox.Ef/Api/DateOnlyJsonConverter.cs` |
| The complete example of an application on the foundation (Core → Db → Domain → Api), tested from the repository to HTTP | `SampleApp`, `CustomerMerge`, `CustomersController`, `OrdersController` | `../tests/Zerox.Ef/Api/Sample/` |
