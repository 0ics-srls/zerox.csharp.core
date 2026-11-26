§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[16-VALUES-ON-LOAD-OPTIONS]
@state::EXEC{run-202611261451,block 01/3 done✓ (RED✓GREEN✓VERIFY✓CROSS✓MB✓);blocks 02,03 pending}
@feature::16-values-on-load-options
@branch::feature/16-values-on-load-options (from feature/15-hierid-parent-name)
@plan::tasks/16-values-on-load-options/{notes,plan}.md{TDDAB#3}
@why::©User 2026-11-26 (from carta-modelli,consumer by submodule):"ValuesAsync deve accettare lo stesso dataloadoptions";EntityService<T>.ValuesAsync took no options and read Repository.Query()→skipped SearchSource() (a narrowed service leaked the values of every row)
!decisions©User::one way only:ValuesAsync(column,LoadOptions)+POST values/{column} with body,GET removed (breaking,README);new branch so earlier consumers keep theirs
!design::only Filter of the options is read (grid options sent as they are);answer = TextValue(Value,Count) new in Ef.Core (©User:"abbiamo anche la cardinalità");values = Search.LoadAsync grouped on the column (keys+counts),on SearchSource(),at most MaxTake;old DistinctTextValues.ValuesAsync(query,column,ct) removed (one implementation)
!review::written in a session of carta-modelli;LSAI ¬ready there (needs dotnet restore)→symbols read from source,low confidence;j-review-plan ¬run on this plan
[DECISIONS DURING EXECUTION-01]
>fixed-01::empties excluded BY THE FILTER,not after the take ← DevExtreme's Take bounds the GROUPS and null/"" come first in key order (measured:Take=2 returned only the null+"" groups;sketch's Take=maxTake+in-memory filter answered an EMPTY list);DistinctTextValues.ValuesAsync adds [column,"<>",""] and [column,"<>",null] to the client's filter (AND,binary form) so Take in SQL bounds the values themselves;counts unchanged (a null/empty row has no value)
!env-01::ast-grep rules were missing in this checkout (.ai-agent submodule uninitialized,.claude links absent)→git submodule update --init .ai-agent + junction .claude/lint-rules→.ai-agent/.claude/lint-rules;ast-grep scan 0

[13-STANDS-ALONE]
@why::©User 2026-11-06 "sto aspettando per partire con progetto nuovo con smgen e zerox":fresh clone+new project with real submodules
>fixed::suspend from another thread between the ring's look and the run reported a false job failure,and TickAsync threw "MainQueue is Suspended" (seen once in 804 tests under a profiler):JobBase.ExecuteAsync decides under its lock,suspended→false (no exception,as a delayed job before its time);Running still throws
>added::README "Use it from a project"
@verified::fresh clone:build,format,807 (779+28 PostgreSQL only),test-postgres on port 53714,pack;new project:build,test,format;merged by fast-forward into main (©User)
!note::tools/verify.sh step 4+ast-grep need .ai-agent setup,¬on a fresh clone;tools/test-postgres.sh default port 55432 lies in the Windows dynamic range

[TASK]
@state::DONE✓{run-20261103-zerox11 6/6,FINAL REVIEW✓}{branch committed,¬pushed}
@feature::11-entity-service (the service layer:EntityService<T>,OpResult<T>,contracts in Core)
@branch::feature/11-entity-service (from feature/10-db-row-version;local,¬pushed)
@plan::tasks/11-entity-service/{notes,index,01-core-domain,02-sample-api}.md{TDDAB#6,parsePlan✓,review✓ (LSAI+xmp4),plan commit 23c18e6}
@block::06✓+FINAL-REVIEW✓{verify.sh[1-3]✓ build,format,781/781;[4/4] lint-run.sh missing here→ast-grep pack rules 0;test-postgres 73/145/100 on port 53714}
@consumer::CcV3 (submodule externals/zerox.csharp.core) adapts by its own plan after this one (TaskItemService:EntityService<TaskItem>,TS client machine,ITaskItemTriggers removed)

[WHY]
©User 2026-10-31 (CcV3 redesign interview)::Zerox.Ef has no service layer (EntityController<T> talks to IRepository<T>:search,CRUD,404/409);an Avalonia UI reuses nothing;entity logic has no place
!decisions::OpResult<T>{Ok,Created,Deleted,NotFound,Conflict,Invalid,Failed,Faulted};Faulted carries ALL exceptions (cancellation too);Failed = domain refused,no HTTP meaning,controller→400;contracts in Zerox.Ef.Core with own load types (Core zero deps);EntityService<T> derivable,registered for every entity,derived services registered too;¬generic SM API (server firing = a case's own service)
!decisions-PROPOSE 2026-11-03::Faulted rethrown by the controller to ErrorHandlingMiddleware (one place logs/500);problem Code stays from the status (Failed→InvalidRequest);LoadOptions copies EVERY DevExtreme member (wire unchanged,schema renamed DataSourceLoadOptionsBase→LoadOptions)
@measured::DevExtreme 5.1.0 LoadResult writes totalCount/groupCount always (-1 default),summary JsonIgnore WhenWritingDefault;Group count/summary same → Loaded/LoadGroup mirror it

[DECISIONS DURING EXECUTION]
!doc::EntityService doc names AddZeroxDomain/AddEntityService as text (a cref to a type not yet written breaks the build)
>deviation-05::MergeInto declares 204+404 only (plan:also 400) ← ZeroxOpenApi documents the 400 on every operation with "The request is invalid: the detail says why."; an explicit 400 replaced it with "Bad Request" → Sample.json unchanged
>fixed-06::wire kept as DevExtreme reads it ← DevExtreme binds Filter with its internal ListConverter and GroupInterval with its internal NumericAndStringConverter (JsonConverter attributes,measured by reflection on 5.1.0):Core GroupingInfo.GroupInterval [JsonConverter(NumberOrStringJsonConverter)] (internal,BCL;schema untyped as before),Domain LoadConversion.FilterOf passes a JSON filter (JsonElements) through DevExtreme's own deserialization of DataSourceLoadOptionsBase (reuse,¬rewrite);tests LoadTypesTests.Should_BindANumericGroupInterval,LoadConversionTests.Should_ReadAJsonFilterAsDevExtremeDoes
!env::tools/test-postgres.sh default port 55432 lies in the Windows dynamic port range → taken by an outbound connection (netstat TIME_WAIT) → every PostgreSQL test "connection refused" (2 runs);ZEROX_EF_POSTGRES_PORT=53714 → green;script unchanged (report to ©User)
!lsai::index keeps deleted files (CustomerMerge.cs) until reopened → absence proved by ls+git
!env::ast-grep in this checkout → scratch sgconfig with CcV3's .claude/lint-rules (_shared+csharp),same rules (.claude links missing here)

[NEXT]
?CcV3 plan:TaskItemService:EntityService<TaskItem> (server fire example),TS client machine (generated compact),ITaskItemTriggers removed,frontend body() LoadOptions,zerox pointer→feature/11-entity-service (push zerox first,©User)

[12-MASSIVE-WITH-RETRY]
@state::DONE✓{branch feature/12-massive-with-retry from feature/11-entity-service,¬pushed until ©User says}
@why::CcV3 05 found OpenMassiveAsync broken on a context with EnableRetryOnFailure:transaction opened outside the execution strategy→first ExecuteUpdate throws "does not support user-initiated transactions";no zerox test ever used a retrying strategy
>added::IRepository<T>.RunMassiveAsync(work,ct){CreateExecutionStrategy().ExecuteAsync:open,work,commit;throw=rollback;transient=rolled back+run again with a new handler};OpenMassiveAsync refused at once when RetriesOnFailure,naming RunMassiveAsync;MassiveRunTests#8 (RetryingStrategy via ReplaceService<IExecutionStrategyFactory>);catalog row
@verified::verify.sh [1-3] build 0/0,format,789/789;[4/4] lint-run.sh missing here→ast-grep with CcV3's pack rules 0;test-postgres port 53714:81/145/100
