§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DEVELOP (CVM run-20261026-zerox-06 from the assetforge session)
@feature::06-port-scheduler (stage E:cyclic job scheduler + WorkingProcess;A–D ✓)
@branch::feature/06-port-scheduler (from feature/05-port-avalonia)
@plan::tasks/06-port-scheduler/index.md (TDDAB multi-file,4 blocks,33 red lines;notes.md = inventory + code organization)
@decisions::©User>scheduler wanted in the unified system (7 years,500 machines,never crashed);model kept as is (composite rings on CircularLinkedList,main ring = slow queue + main jobs + custom queues → one slow job per main lap,priority ring one per tick,one-time + delayed,groups);sync jobs;errors = supervisor (queue with handler → scheduler errors channel JobError),engine failure → process dies
@design::Scheduling/{JobKind,JobState,IJob,JobBase,Job,DelayedJob,JobQueue (onJobError),JobError,JobScheduler (old API names,Tick,Errors,Suspend/Resume/Reset,unknown name → ArgumentException),WorkingProcess (PeriodicTimer on TimeProvider,first cycle at once,stats,StartDetached)}

[NEXT]
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
✗dropped::ActionsContext (product actors LocalOperator/RemoteOPC)
?stage D 05-port-avalonia (PropertyContainer pump on NotifyingObject,async commands,observable task,dialogs/message boxes,charts of the statistics)
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
