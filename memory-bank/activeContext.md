§MBEL:5.0

# CURRENT TASK ONLY — overwrite, do not append

[TASK]
@state::DEVELOP (CVM run-20261026-zerox-05;4/4 blocks done,final review next)
@feature::05-port-avalonia (stage D:UI base on Avalonia 12.1.4;A ✓,B ✓,C ✓)
@branch::feature/05-port-avalonia (from feature/04-port-messaging-editing)
@plan::tasks/05-port-avalonia/index.md (TDDAB multi-file,4 blocks,37 red lines;notes.md = inventory + code organization)
@decisions::©User>D = UI base (PropertyContainer + pump,Command,AsyncCommand + ObservableTask + cancel + timeout + progress,minimal DialogService);charts → own stage later (ScottPlot.Avalonia 5);ActionsContext dropped;scheduler → stage E
@probe::headless 2026-10-26 proved Post+RunJobs,FakeTimeProvider timer posting to UI,ShowDialog closed by Button.ClickEvent,async void exception → Dispatcher.UIThread.UnhandledException
@design::ComponentModel/{PropertyContainer : NotifyingObject (MemberChanged at once,PropertyChanged posted to UI,throttle+debounce on TimeProvider,SetComplexProperty lets the old value go),ThrottlePump one timer per (clock,interval)};Commands/{ICommandWithInfo,Command<T>,AsyncCommand<T> (Execution ObservableTask,CancelCommand,IsTimedOut,failure → caller|dispatcher),UiProgress};Dialogs/{IDialogService + extensions,DialogService,MessageWindow}

[NEXT]
?stage E 06-port-scheduler (©User 2026-10-26:wanted in the unified system):old Commands/Scheduler/Job.cs + WorkingProcess ported with the same model (composite rings on CircularLinkedList,main ring holds main jobs + slow queue → one slow job per main lap,priority ring one job per tick,one-time + delayed jobs,groups suspend/resume,sync jobs);WorkingProcess = the loop container on TimeProvider with stats;errors = supervisor:job exception → errors channel (job,exception,time) the owner reads,engine failure → process dies (explicit P9 exception);fix CreateCustomJob adding PriorityGroup,priority skipped when main throws;tests deterministic (ticks by hand,fake clock)
?charts (ChartSeriesSource,palettes,histogram curves) = own stage on ScottPlot.Avalonia 5 when a view needs them
✗dropped::ActionsContext (product actors LocalOperator/RemoteOPC)
?stage D 05-port-avalonia (PropertyContainer pump on NotifyingObject,async commands,observable task,dialogs/message boxes,charts of the statistics)
?remote::github repo created by the user;then assetforge commits externals/zerox.csharp.core
