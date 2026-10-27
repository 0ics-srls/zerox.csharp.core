§MBEL:5.0

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
