§MBEL:5.0

# Active task progress only

[11-entity-service]
@plan::tasks/11-entity-service/index.md{TDDAB#6}
@execution::run-20261103-zerox11
01-core-contracts✓{Core:OpStatus+OpResult<T>(IsSuccess)+OpResult factories (blank reason→ArgumentException,null exception→ArgumentNullException),LoadOptions (every DataSourceLoadOptionsBase member,IReadOnlyList collections)+SortingInfo+GroupingInfo+SummaryInfo,Loaded+LoadGroup (DevExtreme JSON names,null summary/count left out),IEntityService<T>;OpResultTests#9(+theory rows),LoadTypesTests#3,SurfaceTests+1 (Core references only the runtime);catalog 3 rows}{build✓,format✓,ast-grep✓,Domain 96+4skip,fast713/713}
02-domain-load-conversion✓{Domain:LoadConversion (internal) ToDevExtreme (every member by name,collections→arrays/List,null stays null)+ToLoaded (rows as is,DevExtreme Group→LoadGroup recursive,counts+summary copied,null data→empty);LoadConversionTests#7 (reflection:every DevExtreme member exists+crosses)}{build✓,format✓,ast-grep✓,Domain 103+4skip,fast720}
03-domain-entity-service✓{Domain:EntityService<T> public derivable (Load via LoadConversion+Search.LoadAsync,Values,Get,Create,Update at version,Delete at version;protected Repository,MaxTake,SearchSource,RunAsync (InvalidRequest→Invalid,StaleVersion+lock conflict→Conflict "The row was changed by another user.",else→Faulted incl. cancellation),SaveUnlessMissingAsync moved (controller copy until 06));OptimisticLockConflict.Is;EntityServiceTests#26,OptimisticLockConflictTests#2;catalog 2 rows}{build✓,format✓,ast-grep✓,Domain 129+7skip,PostgreSQL focused 29/29,fast749}
04-domain-registration✓{Domain:ServiceCollectionExtensions.AddZeroxDomain (TryAdd open generic IEntityService<>→EntityService<>,scoped)+AddEntityService<T,TContract,TService> (TService scoped,TContract+IEntityService<T> factories→same instance;closed wins over open generic whatever order);ServiceRegistrationTests#7 (ValidateScopes+ValidateOnBuild);README step 3+catalog row}{build✓,format✓,ast-grep✓,Domain 136+7skip,fast756}
