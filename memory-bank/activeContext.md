§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DEVELOP⚡{/j-cvm-exec-plan,execution:run-20261103-zerox11}
@feature::11-entity-service (the service layer:EntityService<T>,OpResult<T>,contracts in Core)
@branch::feature/11-entity-service (from feature/10-db-row-version;local,¬pushed)
@plan::tasks/11-entity-service/{notes,index,01-core-domain,02-sample-api}.md{TDDAB#6,parsePlan✓,review✓ (LSAI+xmp4),plan commit 23c18e6}
@block::01✓,02✓,03✓,04✓,05✓
@consumer::CcV3 (submodule externals/zerox.csharp.core) adapts by its own plan after this one (TaskItemService:EntityService<TaskItem>,TS client machine,ITaskItemTriggers removed)

[WHY]
©User 2026-10-31 (CcV3 redesign interview)::Zerox.Ef has no service layer (EntityController<T> talks to IRepository<T>:search,CRUD,404/409);an Avalonia UI reuses nothing;entity logic has no place
!decisions::OpResult<T>{Ok,Created,Deleted,NotFound,Conflict,Invalid,Failed,Faulted};Faulted carries ALL exceptions (cancellation too);Failed = domain refused,no HTTP meaning,controller→400;contracts in Zerox.Ef.Core with own load types (Core zero deps);EntityService<T> derivable,registered for every entity,derived services registered too;¬generic SM API (server firing = a case's own service)
!decisions-PROPOSE 2026-11-03::Faulted rethrown by the controller to ErrorHandlingMiddleware (one place logs/500);problem Code stays from the status (Failed→InvalidRequest);LoadOptions copies EVERY DevExtreme member (wire unchanged,schema renamed DataSourceLoadOptionsBase→LoadOptions)
@measured::DevExtreme 5.1.0 LoadResult writes totalCount/groupCount always (-1 default),summary JsonIgnore WhenWritingDefault;Group count/summary same → Loaded/LoadGroup mirror it

[DECISIONS DURING EXECUTION]
!doc::EntityService doc names AddZeroxDomain/AddEntityService as text (a cref to a type not yet written breaks the build)
>deviation-05::MergeInto declares 204+404 only (plan:also 400) ← ZeroxOpenApi documents the 400 on every operation with "The request is invalid: the detail says why."; an explicit 400 replaced it with "Bad Request" → Sample.json unchanged
!lsai::index keeps deleted files (CustomerMerge.cs) until reopened → absence proved by ls+git
!env::ast-grep in this checkout → scratch sgconfig with CcV3's .claude/lint-rules (_shared+csharp),same rules (.claude links missing here)

[NEXT]
?06-api-over-service → FINAL REVIEW (tools/verify.sh && tools/test-postgres.sh)
