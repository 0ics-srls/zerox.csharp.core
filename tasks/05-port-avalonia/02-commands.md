# Commands

<block id="02-command">
## TDDAB-2: Port the Command with Its Information

<intro>
Delivers, in `src/Zerox.Core.Avalonia/Commands`:
- `ICommandWithInfo : ICommand` with `string? Label` and `string? Icon` (an icon resource key):
  what menus and toolbars see of a command of either kind (block 03 implements it too);
- `Command<T>` (sealed): `Command(Action<T?> execute, Func<T?, bool>? canExecute = null,
  string? label = null, string? icon = null, INotifyPropertyChanged? requery = null)`;
  `CanExecute(T?)` = not running AND `canExecute` (true when null); `Execute(T?)` → runs when it
  can and returns true, false when it cannot (also when called again from inside its own action);
  an exception of the action reaches the caller (P9); `RaiseCanExecuteChanged()` posts
  `CanExecuteChanged` to the UI thread; every `PropertyChanged` of the `requery` source re-queries
  (Avalonia has no `CommandManager`: the view model whose state decides is given as the source);
  `ICommand` members cast the parameter: null → `default(T)`, another type → `ArgumentException`
  (the old `as T` turned it silently into null);
- `Command` (static): `Create(Action execute, Func<bool>? canExecute = null, string? label = null,
  string? icon = null, INotifyPropertyChanged? requery = null)` → `Command<object?>`;
- internal `CommandParameter.Cast<T>(object?)`, shared with block 03.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/CommandTP.cs`, `Command.cs`,
`ICommandWithInfo.cs` (WPF `CommandManager.RequerySuggested`, `Icon` as a MahApps `IconKind` enum,
`Tag` with a dummy activable, a `confirm` function, `ApplicationException` without a dispatcher).
No old tests (the command tests were for the auth commands, not ported). Dropped: `Tag`, the
synchronous `confirm` (a confirmation that asks the user is a dialog: async, block 03),
`DummyCommand` / `EmptyCommandArray` statics.

Files: `src/Zerox.Core.Avalonia/Commands/ICommandWithInfo.cs`, `Command.cs` (generic class + static
factory), `CommandParameter.cs`; tests `tests/Zerox.Core.Avalonia.Tests/Commands/CommandTests.cs`.
</intro>

<red>
- test: Should_RunTheAction_WhenItCanExecute — `Execute("a")` returns true and the action got "a"
- test: Should_NotRun_WhenItCannotExecute — `canExecute` false: `CanExecute` false, `Execute` returns false, the action did not run
- test: Should_RefuseItself_WhenExecutedFromInsideItsAction — a nested `Execute` inside the action returns false; the outer one true
- test: Should_ReQueryOnTheUiThread_WhenTheSourceChanges — a `NotifyingObject` source changes on another thread: after `RunJobs()` `CanExecuteChanged` was raised on the UI thread
- test: Should_KeepLabelAndIcon_WhenCreated — `Command.Create(..., label: "Save", icon: "save")`: `Label` "Save", `Icon` "save", seen through `ICommandWithInfo`
- test: Should_RejectTheParameter_WhenItHasAnotherType — `((ICommand)new Command<string>(...)).Execute(42)` → `ArgumentException`; `Execute(null)` passes null
- test: Should_LetTheExceptionReachTheCaller_WhenTheActionThrows — the action throws `InvalidOperationException`: `Execute` throws it and the command can run again
</red>

### Implementation

```csharp
using System.Windows.Input;

namespace Zerox.Core.Avalonia.Commands;

// What menus and toolbars see of a command.
public interface ICommandWithInfo : ICommand
{
    string? Label { get; }

    // An icon resource key.
    string? Icon { get; }
}
```

```csharp
namespace Zerox.Core.Avalonia.Commands;

internal static class CommandParameter
{
    // null → default; another type is a misuse.
    public static T? Cast<T>(object? parameter) => parameter switch
    {
        null => default,
        T value => value,
        _ => throw new ArgumentException($"The command takes a {typeof(T).Name}, not a {parameter.GetType().Name}", nameof(parameter)),
    };
}
```

```csharp
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Commands;

// A synchronous command; the requery source's changes re-query CanExecute (Avalonia has no CommandManager).
public sealed class Command<T> : ICommandWithInfo
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _running;

    public Command(Action<T?> execute, Func<T?, bool>? canExecute = null, string? label = null, string? icon = null, INotifyPropertyChanged? requery = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
        Label = label;
        Icon = icon;
        if (requery is not null)
            requery.PropertyChanged += (_, _) => RaiseCanExecuteChanged();
    }

    public event EventHandler? CanExecuteChanged;

    public string? Label { get; }

    public string? Icon { get; }

    public bool CanExecute(T? parameter) => !_running && (_canExecute?.Invoke(parameter) ?? true);

    // Runs when it can; false when it cannot.
    public bool Execute(T? parameter)
    {
        if (!CanExecute(parameter))
            return false;
        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            _execute(parameter);
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
        return true;
    }

    // Posted to the UI thread: the bindings re-read CanExecute there.
    public void RaiseCanExecuteChanged() => Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));

    bool ICommand.CanExecute(object? parameter) => CanExecute(CommandParameter.Cast<T>(parameter));

    void ICommand.Execute(object? parameter) => Execute(CommandParameter.Cast<T>(parameter));
}

public static class Command
{
    public static Command<object?> Create(Action execute, Func<bool>? canExecute = null, string? label = null, string? icon = null, INotifyPropertyChanged? requery = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new(_ => execute(), canExecute is null ? null : _ => canExecute(), label, icon, requery);
    }
}
```

<success>
- [ ] `CommandTests` pass (7 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="03-async-command">
## TDDAB-3: Port the Async Command and the Observable Task

<intro>
Delivers, in `src/Zerox.Core.Avalonia/Commands`:
- `ObservableTask` (sealed, derives from block 01's `PropertyContainer`: its notifications reach the
  bindings on the UI thread): `Task` (the observed task), `IsRunning`, `IsCompleted`,
  `IsCanceled`, `IsSuccessfullyCompleted`, `IsFaulted`, `Exception` (the first inner exception);
  `ObserveAsync(Task task)`: sets `Task`, notifies every property (null name), awaits the end WITHOUT
  throwing (`ConfigureAwaitOptions.SuppressThrowing | ContinueOnCapturedContext`: the outcome stays in
  the task — whoever awaits the task gets the failure; the old catch-all swallowed it), notifies again;
- `AsyncCommand<T>` (sealed, `ICommandWithInfo`): `AsyncCommand(Func<T?, CancellationScope, Task> execute,
  Terminator terminator, Func<T?, bool>? canExecute = null, Func<T?, Task<bool>>? confirm = null,
  TimeSpan? timeout = null, string? label = null, string? icon = null, INotifyPropertyChanged? requery = null)`;
  `Execution` (`ObservableTask`; the old property was named `Task`, a name that hides the type inside
  the class); `CancelCommand` (`ICommand`: can execute while a run's scope is active; executes
  `Cancel()` on it); `IsTimedOut` (the last run ended by its timeout);
  `CanExecute(T?)` = not starting, not running, `canExecute`;
  `ExecuteAsync(T? parameter = default)` → `Task<bool>`: false when it cannot run or the
  confirmation says no; otherwise a new scope from the `Terminator` (with the timeout), the work
  observed by `Execution`, true at the end — a cancellation (cancel command or timeout) is an
  expected end (no exception), a FAILURE of the work reaches the caller of `ExecuteAsync` (P9);
  `ICommand.Execute` (from a binding, no caller) is `async void` on the UI thread: a failure reaches
  the dispatcher and stops the application (P9); `CanExecuteChanged` posted to the UI thread when a
  run starts and ends and when the `requery` source changes;
- `AsyncCommand` (static): `Create(Func<CancellationScope, Task> execute, Terminator terminator,
  Func<bool>? canExecute = null, TimeSpan? timeout = null, string? label = null, string? icon = null,
  INotifyPropertyChanged? requery = null)` → `AsyncCommand<object?>`;
  `WithProgress<T, TProgress>(Func<T?, CancellationScope, IProgress<TProgress>, Task> execute,
  Action<TProgress> onProgress, Terminator terminator, Func<T?, bool>? canExecute = null,
  TimeSpan? timeout = null, string? label = null, string? icon = null)` → `AsyncCommand<T>` whose
  progress handler runs on the UI thread (internal `UiProgress<T>`: `Report` posts to the `Dispatcher`);
- internal `CancelCommand` (`ICommand`): `Begin(scope)`, `End()`, `CanExecuteChanged` posted.

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/AsyncCommandTP.cs`, `AsyncCommand.cs`,
`IAsyncCommandTP.cs`, `ObservableTask.cs`, `Internal/CancelAsyncCommand.cs`,
`ProgressiveAsyncCommandTP.cs` (WPF `CommandManager`, `BootStrapper.GetTerminator()`,
`SafeFireAndForget`, an `exceptionFunc` callback, a catch-all in `ObservableTask`). No old tests
(the WPF sample `Zerox.Wpf.XTest.AsyncCommand` is an app, not tests). Fixed: the failure of the
work was swallowed (now caller or dispatcher); `ErrorMessage`/`InnerException` duplicates dropped;
a second `Execute` during the confirmation could start a second run (now `_starting`).

Files: `src/Zerox.Core.Avalonia/Commands/ObservableTask.cs`, `AsyncCommand.cs` (generic class +
static factory), `CancelCommand.cs`, `UiProgress.cs`; tests
`tests/Zerox.Core.Avalonia.Tests/Commands/ObservableTaskTests.cs`,
`tests/Zerox.Core.Avalonia.Tests/Commands/AsyncCommandTests.cs` (a `Terminator` on a
`FakeTimeProvider`; work gated by a `TaskCompletionSource`; posted jobs drained with
`Dispatcher.UIThread.RunJobs()`, repeated while a condition is not met, bounded; no sleep).
</intro>

<red>
- test: Should_FollowTheTask_WhenItRunsAndEnds — observing a gated task: `IsRunning` true, `IsCompleted` false; the gate opened and the observation awaited: `IsSuccessfullyCompleted` true; `PropertyChanged` with a null name raised twice (after `RunJobs()`)
- test: Should_KeepTheFailureInTheTask_WhenTheObservedTaskFails — the observed task throws `InvalidOperationException`: `ObserveAsync` completes without throwing, `IsFaulted` true, `Exception` is that exception, awaiting `Task` throws it
- test: Should_ReportRunning_WhenTheWorkRuns — during the run `CanExecute` false and `Execution.IsRunning` true; after it `ExecuteAsync` returned true and `CanExecute` true
- test: Should_NotStartTwice_WhenExecutedDuringItsRun — a second `ExecuteAsync` while the first runs returns false and the work ran once
- test: Should_NotRun_WhenTheConfirmationSaysNo — the confirmation returns false: `ExecuteAsync` false, the work never ran, `CanExecute` true again
- test: Should_CancelTheWork_WhenTheCancelCommandRuns — work `scope.DelayAsync(1 h)`: `CancelCommand.CanExecute` true during the run; `CancelCommand.Execute(null)` → `ExecuteAsync` returns true without throwing, `Execution.IsCanceled` true, `IsTimedOut` false, `CancelCommand.CanExecute` false after
- test: Should_TimeOut_WhenTheTimeoutElapses — timeout 5 s on the fake clock, work `scope.DelayAsync(1 h)`: `Advance(5 s)` → the run ends, `Execution.IsCanceled` true, `IsTimedOut` true
- test: Should_LetTheFailureReachTheCaller_WhenTheWorkThrows — the work throws `InvalidOperationException`: `ExecuteAsync` throws it, `Execution.IsFaulted` true, `CanExecute` true again
- test: Should_LetTheFailureReachTheDispatcher_WhenRunFromABinding — `((ICommand)command).Execute(null)` with failing work: `Dispatcher.UIThread.UnhandledException` receives the `InvalidOperationException` (the test marks it handled)
- test: Should_ReportProgressOnTheUiThread_WhenTheWorkReports — `WithProgress`: the work reports 5 from `Task.Run`; the handler got 5 on the UI thread
- test: Should_RaiseCanExecuteChanged_WhenTheRunStartsAndEnds — after a run and `RunJobs()`, `CanExecuteChanged` was raised at least twice, on the UI thread
</red>

### Implementation

`ObservableTask.cs`:

```csharp
using Zerox.Core.Avalonia.ComponentModel;

namespace Zerox.Core.Avalonia.Commands;

// A task the bindings can follow; its outcome stays in the task (whoever awaits it gets the failure).
public sealed class ObservableTask : PropertyContainer
{
    public Task? Task { get; private set; }

    public bool IsRunning => Task is { IsCompleted: false };

    public bool IsCompleted => Task?.IsCompleted ?? false;

    public bool IsCanceled => Task?.IsCanceled ?? false;

    public bool IsSuccessfullyCompleted => Task?.Status == TaskStatus.RanToCompletion;

    public bool IsFaulted => Task?.IsFaulted ?? false;

    public Exception? Exception => Task?.Exception?.InnerException;

    public async Task ObserveAsync(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Task = task;
        NotifyPropertyChanged(propertyName: null);
        await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        NotifyPropertyChanged(propertyName: null);
    }
}
```

`CancelCommand.cs`:

```csharp
using System.Windows.Input;
using Avalonia.Threading;
using Zerox.Core.Cancellation;

namespace Zerox.Core.Avalonia.Commands;

// Cancels the scope of the run in progress.
internal sealed class CancelCommand : ICommand
{
    private CancellationScope? _scope;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _scope is { IsActive: true };

    public void Execute(object? parameter)
    {
        _scope?.Cancel();
        Changed();
    }

    public void Begin(CancellationScope scope)
    {
        _scope = scope;
        Changed();
    }

    public void End()
    {
        _scope = null;
        Changed();
    }

    private void Changed() => Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
}
```

`UiProgress.cs`:

```csharp
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Commands;

// Progress whose handler runs on the UI thread, whatever thread reports.
internal sealed class UiProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => Dispatcher.UIThread.Post(() => handler(value));
}
```

`AsyncCommand.cs`:

```csharp
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Threading;
using Zerox.Core.Cancellation;

namespace Zerox.Core.Avalonia.Commands;

// An async command: one run at a time, observed by Execution, cancellable, with an optional timeout.
public sealed class AsyncCommand<T> : ICommandWithInfo
{
    private readonly Func<T?, CancellationScope, Task> _execute;
    private readonly Terminator _terminator;
    private readonly Func<T?, bool>? _canExecute;
    private readonly Func<T?, Task<bool>>? _confirm;
    private readonly TimeSpan? _timeout;
    private readonly CancelCommand _cancel = new();
    private bool _starting;

    public AsyncCommand(
        Func<T?, CancellationScope, Task> execute,
        Terminator terminator,
        Func<T?, bool>? canExecute = null,
        Func<T?, Task<bool>>? confirm = null,
        TimeSpan? timeout = null,
        string? label = null,
        string? icon = null,
        INotifyPropertyChanged? requery = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(terminator);
        _execute = execute;
        _terminator = terminator;
        _canExecute = canExecute;
        _confirm = confirm;
        _timeout = timeout;
        Label = label;
        Icon = icon;
        if (requery is not null)
            requery.PropertyChanged += (_, _) => RaiseCanExecuteChanged();
    }

    public event EventHandler? CanExecuteChanged;

    public string? Label { get; }

    public string? Icon { get; }

    public ObservableTask Execution { get; } = new();

    public ICommand CancelCommand => _cancel;

    // The last run ended by its timeout.
    public bool IsTimedOut { get; private set; }

    public bool CanExecute(T? parameter) => !_starting && !Execution.IsRunning && (_canExecute?.Invoke(parameter) ?? true);

    // False when it cannot run or the confirmation says no. A cancellation is an expected end; a failure of the work reaches the caller.
    public async Task<bool> ExecuteAsync(T? parameter = default)
    {
        if (!CanExecute(parameter))
            return false;
        _starting = true;
        RaiseCanExecuteChanged();
        try
        {
            if (_confirm is not null && !await _confirm(parameter).ConfigureAwait(true))
                return false;
            using var scope = _terminator.CreateScope(_timeout);
            _cancel.Begin(scope);
            try
            {
                var work = _execute(parameter, scope);
                var observed = Execution.ObserveAsync(work);
                _starting = false;
                RaiseCanExecuteChanged();
                await observed.ConfigureAwait(true);
                IsTimedOut = scope.State == CancellationState.TimedOut;
                if (work.IsFaulted)
                    await work.ConfigureAwait(true);   // the failure reaches the caller
                return true;
            }
            finally
            {
                _cancel.End();
            }
        }
        finally
        {
            _starting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));

    bool ICommand.CanExecute(object? parameter) => CanExecute(CommandParameter.Cast<T>(parameter));

    // From a binding there is no caller: the run is async void on the UI thread, so a failure of the work
    // reaches the dispatcher and stops the application (P9).
    async void ICommand.Execute(object? parameter) => await ExecuteAsync(CommandParameter.Cast<T>(parameter)).ConfigureAwait(true);
}

public static class AsyncCommand
{
    public static AsyncCommand<object?> Create(
        Func<CancellationScope, Task> execute,
        Terminator terminator,
        Func<bool>? canExecute = null,
        TimeSpan? timeout = null,
        string? label = null,
        string? icon = null,
        INotifyPropertyChanged? requery = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new((_, scope) => execute(scope), terminator, canExecute is null ? null : _ => canExecute(), null, timeout, label, icon, requery);
    }

    // The progress handler runs on the UI thread, whatever thread reports.
    public static AsyncCommand<T> WithProgress<T, TProgress>(
        Func<T?, CancellationScope, IProgress<TProgress>, Task> execute,
        Action<TProgress> onProgress,
        Terminator terminator,
        Func<T?, bool>? canExecute = null,
        TimeSpan? timeout = null,
        string? label = null,
        string? icon = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(onProgress);
        var progress = new UiProgress<TProgress>(onProgress);
        return new((parameter, scope) => execute(parameter, scope, progress), terminator, canExecute, null, timeout, label, icon);
    }
}
```

<success>
- [ ] `ObservableTaskTests` and `AsyncCommandTests` pass (11 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
