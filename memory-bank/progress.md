§MBEL:5.0

# Active task progress only

[11-entity-service]
@plan::tasks/11-entity-service/index.md{TDDAB#6}
@execution::run-20261103-zerox11
01-core-contracts✓{Core:OpStatus+OpResult<T>(IsSuccess)+OpResult factories (blank reason→ArgumentException,null exception→ArgumentNullException),LoadOptions (every DataSourceLoadOptionsBase member,IReadOnlyList collections)+SortingInfo+GroupingInfo+SummaryInfo,Loaded+LoadGroup (DevExtreme JSON names,null summary/count left out),IEntityService<T>;OpResultTests#9(+theory rows),LoadTypesTests#3,SurfaceTests+1 (Core references only the runtime);catalog 3 rows}{build✓,format✓,ast-grep✓,Domain 96+4skip,fast713/713}
02-domain-load-conversion✓{Domain:LoadConversion (internal) ToDevExtreme (every member by name,collections→arrays/List,null stays null)+ToLoaded (rows as is,DevExtreme Group→LoadGroup recursive,counts+summary copied,null data→empty);LoadConversionTests#7 (reflection:every DevExtreme member exists+crosses)}{build✓,format✓,ast-grep✓,Domain 103+4skip,fast720}
