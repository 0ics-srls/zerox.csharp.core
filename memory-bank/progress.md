§MBEL:5.0

# Active task progress only

[05-port-avalonia]
✓notes.md + plan (4 blocks,37 red lines);review:parsePlan valid,red count matches,headless probe green
✓01 property-container:Avalonia ComponentModel/{PropertyContainer : NotifyingObject (MemberChanged at once,PropertyChanged posted to UI,throttle+debounce,StopEventThrottling sends waiting,EventsOff/On/OnAndNotifyAll,SetComplexProperty lets the old value go,Dispose),ThrottlePump (one TimeProvider timer per (clock,interval),tick posts flush),PendingChange};12 tests (4 ported,deterministic on fake clock + RunJobs);leak test fixed (old asserted IsAlive)
!gotcha::Avalonia analyzer AVA2001 flags any OnPropertyChanged override not calling base → suppressed on NotifyingObject's seam with reason
✓02 command:Avalonia Commands/{ICommandWithInfo (Label,Icon key),Command<T> + static Command.Create (requery from an INotifyPropertyChanged source,CanExecuteChanged posted to UI,Execute returns bool,reentrancy refused,exception reaches caller),CommandParameter.Cast (other type → ArgumentException)};7 tests
✓03 async-command:Avalonia Commands/{ObservableTask : PropertyContainer (SuppressThrowing|ContinueOnCapturedContext,outcome stays in the task),AsyncCommand<T> (Execution,CancelCommand,IsTimedOut,confirm async,_starting guard,failure → ExecuteAsync caller or dispatcher via async void ICommand.Execute,cancel/timeout = expected end) + static AsyncCommand.Create/WithProgress,CancelCommand,UiProgress (Report posted to UI)};11 tests,3 runs stable
✓04 dialogs:Avalonia Dialogs/{MessageKind,IDialogService.ShowAsync,DialogServiceExtensions (Notify/Confirm/ShowError string|Exception),DialogService (InvokeAsync to UI,modal on owner or plain,internal Opened),MessageWindow (code-built,OK/Cancel,Result)};7 tests
!gotcha::a test that calls from a worker must await the call being made before draining the dispatcher (Task.Factory.StartNew without unwrap) — draining first raced
