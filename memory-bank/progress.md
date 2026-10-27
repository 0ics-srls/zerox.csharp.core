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
