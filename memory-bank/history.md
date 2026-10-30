§MBEL:5.0

# History

## Today
(empty - completed tasks go here)

[2026-10-26 01-scaffold ✓]
✓repository created,.ai-agent submodule + setup,j-settings.md + lint config (sgconfig.yml,Directory.Build.props + .editorconfig templates)
✓plan written:tasks/01-scaffold/plan.md (3 TDDAB blocks)
✓j-review-plan (RepositoryRoot in assetforge's shape)
✓01 solution-skeleton:global.json,version.build (1),Directory.Build.props/targets/Packages.props,.editorconfig (+tests overrides),.gitignore,zerox.csharp.core.slnx,3 empty src + 3 test projects,tests/Shared/RepositoryRoot.cs linked;7 tests green,build 0 warnings,format+ast-grep clean
!gotcha::an XML comment in a .props must not contain '--' (MSB4024 → restore fails with a silent RestoreTask error)
✓02 headless-ui-session:TestApp (plain Application on headless),HeadlessUi.RunAsync (assetforge shape,session per assembly),4 tests + version = 5 green
!gotcha::headless Dispatcher.UIThread.CheckAccess() is true on ANY thread (UIThread bound to first toucher,reset by the session) → recognise the UI thread by SynchronizationContext.Current is AvaloniaSynchronizationContext (null outside);plan RED reworded in place
!fact::HeadlessUnitTestSession runs on a thread-pool thread,completes a plain TaskCompletionSource,rethrows the body's exception unchanged (no AggregateException)
✓03 verify-and-docs:tools/verify.sh ([1/4] build,[2/4] format,[3/4] tests,[4/4] ast-grep;stops at first failure;works from any folder),CLAUDE.md project part (Project,Stack locks,Rules incl. relative paths only,Build & Test),README.md;3 shell checks run from the session scratchpad (not in the repo)
✓FINAL REVIEW:tools/verify.sh 4/4 ok,11 tests,0 warnings

[2026-10-26 02-port-foundations ✓]
✓stage A on feature/02-port (CVM run-20261026-zerox-02,13/13 blocks,verify.sh 4/4,284 tests,0 warnings):Dnf port (148 tests),SortedSearch,BitMask,CircularLinkedList,AtomicBoolean,CriticalSection (LockAsync),Terminator + CancellationScope (TimeProvider),OwnerQueue (§3.7,StartDetached),HierId immutable,NumberRange<T> generic math,TextExtensions + JsonDefaults,ValueConversion,AttributeExtensions + FastProperty
!fixed+pinned::binary search bounds,BitArray == mutating operands,AtomicBoolean callback,HierId mutability/GetParts/Splice,long/ulong limits,Truncate over the limit,FastProperty cache not thread safe
!gotchas::CA1000 → factories on non-generic static class;CA1849/xUnit1031 in async tests;CA1861 constant arrays → static readonly;CA2263 → pass Type via variable;CA1711 suppressed only on OwnerQueue

[2026-10-26 03-port-statistics ✓]
✓stage B on feature/03-port-statistics (CVM run-20261026-zerox-03,8/8 blocks,verify.sh 4/4,370 tests):Interval<T> + CircularBuffer<T> + KeyComparer,ISeriesKeys (TimeKeys monotonic,IndexKeys),ISeries/IValueSeries/SeriesUpdate,DataSeries<TKey,TValue>,ArraySeries + CounterWidening,CyclicSeries (MinMean/MaxMean),SeriesAggregator + SeriesGroup,[Aggregable] + IAggregableSource + AggregableSources,basic RemoteDataProvider (channel + PeriodicTimer)
!fixed+pinned::pooled interval in lazy view,integer rate,first rate vs default key,keys going back,caller array stored,stale channel rate,check-then-add register,id index losing series
!gotchas::CA1716 (VB keywords) → NextKey(),Distance(earlier,later);CA1822 on throwing test getters

[2026-10-26 03/04/05 stages B,C,D ✓]
✓B statistics (370 tests),C notification+validation+edit copy+messaging (402),D Avalonia UI base (439):see each task's notes.md;removed the dependency locks and structural guard tests nobody decided (user)

## 2026-10-27 — activeContext of 06-port-scheduler, replaced by 07 (verbatim)

§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DONE ✓ (CVM run-20261026-zerox-06 4/4,verify 4/4,472 tests);waiting for the user's review
@feature::06-port-scheduler (stage E:cyclic job scheduler + WorkingProcess;A–D ✓)
@branch::feature/06-port-scheduler (from feature/05-port-avalonia)
@plan::tasks/06-port-scheduler/index.md (TDDAB multi-file,4 blocks,33 red lines;notes.md = inventory + code organization)
@decisions::©User>scheduler wanted in the unified system (7 years,500 machines,never crashed);model kept as is (composite rings on CircularLinkedList,main ring = slow queue + main jobs + custom queues → one slow job per main lap,priority ring one per tick,one-time + delayed,groups);sync jobs;errors = supervisor (queue with handler → scheduler errors channel JobError),engine failure → process dies
@design::Scheduling/{JobKind,JobState,IJob,JobBase,Job,DelayedJob,JobQueue (onJobError),JobError,JobScheduler (old API names,Tick,Errors,Suspend/Resume/Reset,unknown name → ArgumentException),WorkingProcess (PeriodicTimer on TimeProvider,first cycle at once,stats,StartDetached)}

[NEXT]
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
✗dropped::ActionsContext (product actors LocalOperator/RemoteOPC)
?stage D 05-port-avalonia (PropertyContainer pump on NotifyingObject,async commands,observable task,dialogs/message boxes,charts of the statistics)
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core

## 2026-10-30 — archived when 10-db-row-version started

### activeContext (07-port-db-api, as it was)

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DONE ✓ (CVM run-20261027-zerox-07 10/10,verify.sh 4/4,all tests green);reviewed 2026-10-27 (3 bugs fixed);pushed to origin;global.json lowered locally (10.0.100) and NEVER committed
@feature::07-port-db-api (the DB → API foundation:context → repository → unit of work → controller,ported from Filo,read-only source `../../home/laco/Gestionale`)
@branch::feature/07-port-db-api (from feature/06-port-scheduler which waits for review;pushed to origin)
@plan::tasks/07-port-db-api/index.md (TDDAB multi-file:01-db.md blocks 01–04,02-domain.md 05–07,03-api.md 08–10;notes.md = request,code organization,inventory source→target,the example)

[DECISIONS ©User 2026-10-27]
@layers::the Zerox.Ef family Zerox.Ef.Core → Zerox.Ef.Db → Zerox.Ef.Domain → Zerox.Ef.Api (©User 2026-10-27:"nascondere tutti sti pezzi in Zerox.Ef.{Core,Db,Domain,Api}",folders = namespaces);an application has the same chain Core → Db → Domain → Api and takes from each its piece;Zerox.Core stays as it was (no data contracts)
@contracts::Zerox.Ef.Core = the family's contracts,no EF type,no dependency (IEntity,IHasOptimisticLock,IRepository<T>,IRepositoryFactory,IUnitOfWork,IMassiveOpHandler<T>,IFieldsToSet<T>);Zerox.Ef.Db = IDbContext (names EF types) + the EF implementation
!app-core::an application with contracts of its own keeps them in ITS Core (only contracts/interfaces);Db = entities + context + db implementation ©User:"se app ha suoi contratti deve avere core"
!nothing-of-filo::no Filo names,hooks,Italian,task numbers;Filo itself unchanged
!idbcontext::everything rests on IDbContext,no base DbContext;project writes `MyContext : DbContext, IDbContext`;state per context in DbSession (ConditionalWeakTable),rules in ONE singleton SaveChangesInterceptor (UseZeroxSession):sync save throws,massive open → throws,boundary open → SuppressWithResult(0),Version++ on Modified
@no-tags::ITag/ITagRepository not ported (Filo workaround);one port IRepository<T> where T : IEntity
@no-auth::EntityController<T> without [Authorize] and without MCP;out of scope MCP,API key,login,roles,shared test library
@controller::EntityController<T> Query/Groups/Projection on DevExtreme DataSourceLoadOptions (server-side filter,sort,paging),Values/{column},CRUD;409 on stale Version via the middleware
@example::tests/Zerox.Ef/Api/Sample/{Core (ICustomerMerge),Db (Customer,Order,SampleDbContext),Domain (CustomerMerge),Api (controllers,SampleApp on TestServer + Sqlite)} tested at every level up to end-to-end HTTP

[REVIEW FINDINGS FIXED]
@verified::EF Core 10 source:SaveChangesStartingAsync runs BEFORE TryDetectChanges (Version++ in the interceptor is saved);SavedChanges fires also for suppressed saves (count only EntitiesSavedCount>0);CoreOptionsExtension.Interceptors exists;plain interceptors do not key the service provider
@sqlite::cannot ORDER BY/SUM decimal → sample maps Amount HasConversion<double>() with the reason
@datasourceresult::DataSourceResult<T> is a Filo type,not DevExtreme → created by block 06 (lowercase names = DevExtreme contract)

[RUN ENVIRONMENT]
!sdk::©User "usa quella che hai" → global.json lowered LOCALLY to 10.0.100 (latestFeature → 10.0.113);NEVER committed (stage by path)
!cvm::run from the Filo session:CVM writes Gestionale/.cvm (Filo's progress archived .211,full backup in its scratchpad)
!links::.claude/support and .claude/lint-rules linked by hand to .ai-agent (gitignored)

[NEXT]
?review of 07 by the user;then merge order (06 waits too)
?with SDK 10.0.400:restore global.json (git checkout global.json) and rerun tools/verify.sh
?06-port-scheduler still waiting for the user's review
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
✓remote::github.com/0ics-srls/zerox.csharp.core private (2026-10-27),every branch pushed
✓review fixes 2026-10-27 (©User):values/{column} refuses a reserved field;PUT loads the stored row → 404 when missing,EntityFields.CopyEditable (no key,navigations,reserved fields) + PinReadVersion;EF 10.0.13 = assetforge's lock
✓decided 2026-10-27 (©User):middleware = top of the request,process never dies (client gone = Information,started response = abort,no second exception);optimistic lock correct everywhere (LastWriterWins with client Version = 409 no retry,DELETE ?version=N on lock entities)

### progress (06, 07, 08, as it was)

# Active task progress only

[06-port-scheduler]
✓notes.md + plan (4 blocks,33 red lines);review:parsePlan valid,red count matches,exact tick counts computed from the ring order
✓01 jobs:Scheduling/{JobKind,JobState,IJob,JobBase (Suspended → Ready ⇄ Running,suspend during run kept,resume during run stays running),Job (Cyclic/Once),DelayedJob (TimeProvider due time)};5 tests (1 ported)
!decision::CA1716 (VB keywords as member names) turned off in .editorconfig:no VB consumer;the old names (Resume) stay
✓02 job-queue:Scheduling/JobQueue (ring on CircularLinkedList,one element per Execute,run-once leaves after run or failure,not-Ready skipped,onJobError = supervisor else rethrow;CA1711 suppressed on the type like OwnerQueue);11 tests (7 ported with the old numbers)
✓03 job-scheduler:Scheduling/{JobError,JobScheduler (main ring = SlowQueue first + main jobs + custom queues,priority ring,Tick one main + one priority,Errors channel from every queue's handler,old Create* API,groups,unknown name/group/queue → ArgumentException,CreateCustomJob without PriorityGroup)};10 tests (5 ported with exact counts by Tick,multithread @Integration);a group exists only once a job joined it
✓04 working-process:Scheduling/WorkingProcess (PeriodicTimer on TimeProvider created in Start,first cycle at once,loop via OwnerQueue.StartDetached,StopAsync cancels+disposes timer+awaits end,restart allowed,CyclesDone/LastCycle/MeanCycle);7 tests on FakeTimeProvider,no sleep;runtime = new WorkingProcess(name,_ => scheduler.Tick(),period,time)
@numbers::verify.sh 4/4,472 tests (285 Zerox.Core),0 warnings

[07-port-db-api]
✓notes.md + plan (10 blocks,136 red lines,TDDAB multi-file);j-review-plan:8 fixes (DataSourceResult created in 06,Sqlite decimal → double conversion in the sample,SavedChanges counts only saves with entities,DELETE-failing DbCommandInterceptor for the half-way failure,TestDb.NewScope + AddDbContext-before-AddZeroxDb refusal,unique Box.Label,AddZeroxJson in the test host,Memo entity,10 depends on 03)
✓layout decided:Zerox.Core/Data contracts,Zerox.Db IDbContext+EF implementation,Zerox.Domain search/validator/last writer wins,Zerox.Api controller/errors/JSON;sample with its own Core (ICustomerMerge)
✓01 db-repository:Zerox.Core/Data/{IEntity,IRepository<T>,IRepositoryFactory};Zerox.Db/{IDbContext,DbSession (ConditionalWeakTable),SessionInterceptor (sync save throws),UseZeroxSession (+typed overload),Repository<T> (refuses a context without the session),RepositoryFactory,AddZeroxDb};14 tests;catalog+README
!found::EF Core 9+ AddDbContext configurations ADD UP (two AddDbContext keep the session) → the refusal test uses a context registered by hand with its own options
!found::EF 10.0.0 pulls SQLitePCLRaw 2.1.11 (NU1903 high,audit as error) → EF packages 10.0.13 (= assetforge's lock,restore clean)
!lint::ast-grep forbids SaveChanges() text and null! → test helper property instead of null!,ast-grep-ignore on the one line that proves the sync save is refused
✓02 db-unit-of-work:Zerox.Core/Data/IUnitOfWork + IRepository.OpenUnitOfWork;Zerox.Db/{DbSession.Boundary (root/slave,ApplyAsync clears the tracker on failure and rethrows,close without applying),UnitOfWork handle,interceptor swallows (SuppressWithResult 0) also a save called directly on the context};10 tests (24 in Zerox.Db.Tests)
!kiss::no Applying flag:the boundary is cleared before the applying save,so the swallow rule needs Boundary only
✓03 db-massive-op:Zerox.Core/Data/{IMassiveOpHandler<T>,IFieldsToSet<T>} + IRepository.OpenMassiveAsync;Zerox.Db/{DbSession.Massive + its transaction (refused with pending changes,inside a unit of work,twice;clears the tracker;commit or rollback on dispose),MassiveOpHandler (FieldsToSet → UpdateSettersBuilder),interceptor throws on save while open};9 tests (33 in Zerox.Db.Tests)
✓04 db-optimistic-lock:Zerox.Core/Data/IHasOptimisticLock + IRepository.{PinReadVersion,DetachAll};Zerox.Db/ModelBuilderExtensions.ApplyOptimisticLock (IsConcurrencyToken,never IsRowVersion),interceptor Version++ on Modified after the swallow rule;9 tests (42 in Zerox.Db.Tests)
✓05 domain-validator:Zerox.Domain (refs Zerox.Db,DevExtreme.AspNet.Data 5.1.0) {InvalidRequestException,LoadOptionsValidator (port 1:1 of the Filo rules,English messages,neutral examples),EntityPaths (Resolve,IsNavigation,Inner,IsReserved),Search.MakeSafe};26 tests,45 cases (one extra:the value messages show the right form)
✓06 domain-search:Zerox.Domain/{Search.RowsAsync/GroupsAsync/ProjectionAsync (MakeSafe,reserved-field check,route errors,validator,LoadAsync = the one rethrow-as-InvalidRequestException,data materialized inside the try),DataSourceResult<T> (lowercase DevExtreme contract),GroupsResult/GroupNode (IReadOnlyList summaries:CA1819),DistinctTextValues};mutation check:without the reserved check 5 red;67 cases in Zerox.Domain.Tests
!found::DevExtreme projection of "Box.Label" returns a NESTED dictionary {"Box":{"Label":…}},not a "Box.Label" key
✓07 domain-last-writer:Zerox.Domain/LastWriterWins(IRepositoryFactory) SaveAsync<T> (tracked read in scope,PinReadVersion on the first attempt only,retry with DetachAll,MaxAttempts 3,null = 404 without retry,no transaction);9 tests;mutation check:without DetachAll 4 red;76 cases in Zerox.Domain.Tests
✓08 api-errors-json:Zerox.Api (FrameworkReference AspNetCore,refs Zerox.Domain) {ApiError/ApiErrorCode,ErrorHandlingMiddleware (409 only with a concurrency token,400 InvalidRequestException not logged as error,500 without details logged),UseZeroxErrorHandling,ZeroxJson.AddZeroxJson (MVC + HTTP options,naming null)/AddConverters,DateOnly converters};TestHost (WebApplication on TestServer + Sqlite + log capture);10 tests;mutation check:409 without the token filter → the 500 test red
!decision::CA1848 none for src/Zerox.Api in .editorconfig (plain logging,no [LoggerMessage]:user rule)
!env::Microsoft.AspNetCore.TestHost 10.0.13 = the installed ASP.NET runtime of this machine
✓09 api-entity-controller:Zerox.Api/EntityController<T> (query/groups/projection on DevExtreme load options,values/{column} guarded,CRUD,409 on stale PUT via the middleware,no [Authorize]);the sample app tests/Zerox.Api.Tests/Sample/{Db (Customer,Order,SampleDbContext with Amount→double for Sqlite),Api (two one-line controllers,SampleApp on TestServer)};18 HTTP tests,29 in Zerox.Api.Tests;mutation check:without the column guard 2 red
!found::the DevExtreme load options bind from the JSON body with System.Text.Json as they are (camelCase keys too):no converter needed
✓10 sample-end-to-end:the example completed by layers — Sample/Core ICustomerMerge (the app's own contract),Sample/Domain CustomerMerge (one unit of work over two repositories),Sample/Api CustomersController.MergeInto + Rename (LastWriterWins) + OrdersController.CloseBefore (massive op),SampleApp registrations;14 tests at three levels (repository,service,HTTP) incl. the whole lifecycle;mutation check:without the unit of work 2 red;README "Building on the database foundation";nothing new under src/
@numbers::verify.sh 4/4 (build,format,tests,ast-grep),0 warnings
✓rename:the four libraries became the Zerox.Ef family (src/Zerox.Ef.{Core,Db,Domain,Api},tests/Zerox.Ef.{Db,Domain,Api}.Tests,namespaces = folders);the contracts left Zerox.Core (Zerox.Core.Data → Zerox.Ef.Core);catalog,README,MB updated;the executed plan keeps the old names (notes.md says so)
✓review 2026-10-27 (©User):3 bugs fixed (values reserved field,PUT wiped reserved fields,PUT missing = 404),optimistic lock correct (LastWriterWins client Version = 409,DELETE ?version=N),middleware = top of the request (process never dies),EF 10.0.13;folders src/Zerox.Ef/{Core,Db,Domain,Api} + tests/Zerox.Ef/{Db,Domain,Api} (one folder per namespace segment,git mv)

[08-async-only]
@branch::feature/08-async-only (from feature/07-port-db-api)
✓Zerox.Ef:SessionInterceptor also IDbCommandInterceptor+IDbConnectionInterceptor+IDbTransactionInterceptor,sync roads → SyncDatabaseAccessException (public,Zerox.Ef.Db);sync save now the same exception;6 SyncAccessTests;whole Ef suite (181) runs under the guard = proof the library never goes sync
!found::Sqlite EnsureCreatedAsync runs HasTables synchronously inside (EF internals) → guard only CommandSource LinqQuery,SaveChanges,FromSqlQuery,ExecuteSqlRaw,ExecuteDelete,ExecuteUpdate
✓Zerox.Core:CriticalSection Lock/Lock(timeout)/TryLock removed (blocking SemaphoreSlim.Wait);PocoProxy ToJson/Populate(string) and JsonDefaults ToJson/FromJson(string) removed (stream async stay);JobScheduler jobs Func<CancellationToken,Task>,IJob.ExecuteAsync,JobQueue takes the job under lock and awaits it outside,TickAsync;WorkingProcess cycle Func<CancellationToken,Task> awaited
✓Zerox.Core.Avalonia:sync Command<T>/Command.Create removed (AsyncCommand covers;ICommand.Execute async void stays,required by ICommand)
@kept::lock around short in-memory state (statistics,circular buffer,job states,channels,validator);sync Dispose of in-memory resources (CTS,semaphore;OwnerQueue.Dispose drains in background,never waits);CPU-only helpers (collections,search,reflection,numbers,validation);IRepository tracker methods (Update,Delete,DeleteRange,PinReadVersion,DetachAll,Local:no I/O);STJ converters (sync by STJ contract)
@numbers::verify.sh 4/4,644 tests
✓review fixes (©User 2026-10-27 "OpenUnitOfWork non può essere senza using"):Take required ≤ DefaultMaxTake 1000 (EntityController.MaxTake) except count/summary;POST with Id → 400;added locked row Version 0 (interceptor);IRepository.Update(T) removed;SessionScopeCheck (AddZeroxDb) closes a unit of work/massive op left open at scope end and throws;mutation check:each fix removed → its tests red;commit e565e58
✓XML docs:/// on the whole Zerox.Ef public surface (380 lines),CS1591 error in the four Ef projects;commit e4b68f9
✓PostgreSQL:tests/Zerox.Ef/TestDatabase.cs linked into the 3 Ef test projects (Sqlite in memory,or a fresh PostgreSQL database per class when ZEROX_EF_TEST_POSTGRES is set,dropped WITH (FORCE));tools/test-postgres.sh (postgres:16-alpine in Docker,[k/5]);192 Ef cases green on PostgreSQL 16 (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3) and Sqlite;negative check:a dead server makes them fail;sample keeps decimal on a real database (double conversion only on Sqlite)
!found::Npgsql EnsureCreatedAsync and every library road stay async under the guard
