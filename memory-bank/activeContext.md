§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::⚡ EXECUTING (CVM run-20261027-zerox-07,Gestionale/.cvm,Filo's 211 progress archived as uplan-progress.211.json);blocks ✓01 ✓02 ✓03 ✓04 (Zerox.Db complete) ✓05 ✓06 ✓07 (Zerox.Domain complete)
@feature::07-port-db-api (the DB → API foundation:context → repository → unit of work → controller,ported from Filo,read-only source `../../home/laco/Gestionale`)
@branch::feature/07-port-db-api (local,from feature/06-port-scheduler which waits for review;never pushed)
@plan::tasks/07-port-db-api/index.md (TDDAB multi-file:01-db.md blocks 01–04,02-domain.md 05–07,03-api.md 08–10;notes.md = request,code organization,inventory source→target,the example)

[DECISIONS ©User 2026-10-27]
@layers::zerox Zerox.Core → Zerox.Db → Zerox.Domain → Zerox.Api;an application has the same chain Core → Db → Domain → Api and takes from each zerox library its piece
@contracts::Zerox.Core/Data = zerox's contracts,no EF type (IEntity,IHasOptimisticLock,IRepository<T>,IRepositoryFactory,IUnitOfWork,IMassiveOpHandler<T>,IFieldsToSet<T>);Zerox.Db = IDbContext (names EF types) + the EF implementation
!app-core::an application with contracts of its own keeps them in ITS Core (only contracts/interfaces);Db = entities + context + db implementation ©User:"se app ha suoi contratti deve avere core"
!nothing-of-filo::no Filo names,hooks,Italian,task numbers;Filo itself unchanged
!idbcontext::everything rests on IDbContext,no base DbContext;project writes `MyContext : DbContext, IDbContext`;state per context in DbSession (ConditionalWeakTable),rules in ONE singleton SaveChangesInterceptor (UseZeroxSession):sync save throws,massive open → throws,boundary open → SuppressWithResult(0),Version++ on Modified
@no-tags::ITag/ITagRepository not ported (Filo workaround);one port IRepository<T> where T : IEntity
@no-auth::EntityController<T> without [Authorize] and without MCP;out of scope MCP,API key,login,roles,shared test library
@controller::EntityController<T> Query/Groups/Projection on DevExtreme DataSourceLoadOptions (server-side filter,sort,paging),Values/{column},CRUD;409 on stale Version via the middleware
@example::tests/Zerox.Api.Tests/Sample/{Core (ICustomerMerge),Db (Customer,Order,SampleDbContext),Domain (CustomerMerge),Api (controllers,SampleApp on TestServer + Sqlite)} tested at every level up to end-to-end HTTP

[REVIEW FINDINGS FIXED]
@verified::EF Core 10 source:SaveChangesStartingAsync runs BEFORE TryDetectChanges (Version++ in the interceptor is saved);SavedChanges fires also for suppressed saves (count only EntitiesSavedCount>0);CoreOptionsExtension.Interceptors exists;plain interceptors do not key the service provider
@sqlite::cannot ORDER BY/SUM decimal → sample maps Amount HasConversion<double>() with the reason
@datasourceresult::DataSourceResult<T> is a Filo type,not DevExtreme → created by block 06 (lowercase names = DevExtreme contract)

[RUN ENVIRONMENT]
!sdk::©User "usa quella che hai" → global.json lowered LOCALLY to 10.0.100 (latestFeature → 10.0.113);NEVER committed (stage by path)
!cvm::run from the Filo session:CVM writes Gestionale/.cvm (Filo's progress archived .211,full backup in its scratchpad)
!links::.claude/support and .claude/lint-rules linked by hand to .ai-agent (gitignored)

[NEXT]
?run /j-cvm-exec-plan from a session in this repository,with SDK 10.0.400
?06-port-scheduler still waiting for the user's review
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
