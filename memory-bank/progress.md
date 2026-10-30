§MBEL:5.0

# Active task progress only

[10-db-row-version]
@plan::tasks/10-db-row-version/index.md{TDDAB#5}
@execution::run-20261030-zerox10rowversion
01-db-row-version✓{ModelBuilderExtensions.ApplyOptimisticLock(ModelBuilder,DatabaseFacade):IHasOptimisticLock branch kept until 05,row versions=IsConcurrencyToken&OnAddOrUpdate,refused unless uint|ulong|long (PostgreSQL:uint only),SQLite:HasDefaultValue(0)+one Warning "Zerox.Ef.Db";.editorconfig CA1848 none for src/Zerox.Ef/{Api,Db};tests/Zerox.Ef/LogCapture.cs shared (linked Db+Api);TestDb:Locked [Timestamp] uint,Wide/Blob/RowVersionDbContext,Use<TContext>;OptimisticLockTests#16;tools/test-postgres.sh passes its arguments (+--ignore-exit-code 8);README+catalog}+fixes{OpenAPI LineBreaks,client lockfile registry-free,vitest native loader}{build✓,format✓,ast-grep✓,SQLite Db58 Domain80 Api75,PostgreSQL 58/80/75,fast671/671}
02-db-client-row-writes✓{IRepository<T>.Update(T,fields):Attach+IsModified only named,¬key,¬generated,unknown name→ArgumentException;TryDeleteById(id,ulong?):Activator stub+row version via EF metadata (Set<T>.EntityType),false when it does not fit uint|long,no version on a locked entity→ArgumentException;HasRowVersion;GetByIdAsync doc without "no Update";ClientRowTests#15;catalog}{build✓,format✓,ast-grep✓,Db73 SQLite,PostgreSQL 73/80/75,fast686/686}
03-domain-editable-stale?
04-api-client-version?
05-remove-app-managed-lock?
