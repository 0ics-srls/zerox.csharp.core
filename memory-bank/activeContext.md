§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DONE stage C (CVM run-20261026-zerox-04,5/5 blocks,verify.sh 4/4,402 tests,0 warnings);next stage D 05-port-avalonia
@feature::04-port-messaging-editing (stage C of 4:A ✓,B ✓,C notification+validation+edit copy+messaging,D Avalonia)
@branch::feature/04-port-messaging-editing (from feature/03-port-statistics)
@plan::tasks/04-port-messaging-editing/index.md (TDDAB multi-file,5 blocks,39 red lines;notes.md = inventory + code organization)
@decisions::©User>minimal NotifyingObject in Zerox.Core (D adds pump+UI thread);FastDeepCloner 1.3.6 + ObjectsComparer 1.4.1 from NuGet in Zerox.Core;channel message in memory only;no dependency locks and no structural guard tests (only tests that prove code works),removed 87c78c3 + 5d1a0dd
@design::ComponentModel/NotifyingObject;Validation/{ValidationRule,ValidationRules,ValidationParameters,IValidatable,Validator<T> (composition,INotifyDataErrorInfo),ValidationException};Editing/{PocoExtensions,MemberDifference,PocoProxy<T>};Messaging/{Message<TKind>,IMessageSource,IMessageDestination,MessageChannel<TKind>};JsonDefaults IncludeFields

[NEXT]
?stage E 06-port-scheduler (©User 2026-10-26:wanted in the unified system):old Commands/Scheduler/Job.cs + WorkingProcess ported with the same model (composite rings on CircularLinkedList,main ring holds main jobs + slow queue → one slow job per main lap,priority ring one job per tick,one-time + delayed jobs,groups suspend/resume,sync jobs);WorkingProcess = the loop container on TimeProvider with stats;errors = supervisor:job exception → errors channel (job,exception,time) the owner reads,engine failure → process dies (explicit P9 exception);fix CreateCustomJob adding PriorityGroup,priority skipped when main throws;tests deterministic (ticks by hand,fake clock)
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
✗dropped::ActionsContext (product actors LocalOperator/RemoteOPC)
?stage D 05-port-avalonia (PropertyContainer pump on NotifyingObject,async commands,observable task,dialogs/message boxes,charts of the statistics)
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
