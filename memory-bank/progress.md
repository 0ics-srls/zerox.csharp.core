§MBEL:5.0

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
!found::EF 10.0.0 pulls SQLitePCLRaw 2.1.11 (NU1903 high,audit as error) → EF packages 10.0.14
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
