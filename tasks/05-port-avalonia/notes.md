# 05-port-avalonia — notes

## Requirement

Stage D of the port of the old Zerox code: the UI base on Avalonia 12.1.4 in `Zerox.Core.Avalonia`.
Source (never edited): `assetforge/tmp/zerox/Zerox.Wpf.Core/Core/Containers/PropertyContainer.cs`,
`MemberContainer.cs`, `IoCEnabledBindableAsync.cs`, `Core/Commands/*` (`Command`, `AsyncCommand`,
`ObservableTask`, `Internal/CancelAsyncCommand`, `ProgressiveAsyncCommand`, `ICommandWithInfo`),
`Core/Wpf/MessagingContext.cs`; tests `Zerox.Wpf.XTest.Core/Core/PropertyContainers/ThrotlingTest.cs`,
`NestedPropertyChangedTest.cs`.

Decisions taken with the user (2026-10-26):
- the D is the UI base: property container with the throttle pump, commands, async commands with
  the observable task and cancellation, a MINIMAL dialog service (notify, confirm, error);
- charts of the statistics → their own stage on ScottPlot.Avalonia 5 when a view needs them;
- `ActionsContext` dropped (product actors LocalOperator/RemoteOPC);
- the cyclic job scheduler + `WorkingProcess` → stage E (same model, errors to a channel).

A probe on the headless platform (2026-10-26, removed after) proved what the design relies on:
`Dispatcher.UIThread.Post` + `RunJobs`; a `FakeTimeProvider` timer whose callback posts to the
dispatcher; `ShowDialog<bool>` closed by raising `Button.ClickEvent`; an exception escaping an
`async void` on the UI thread reaches `Dispatcher.UIThread.UnhandledException`.

## Code Organization

1. **What is shared.** `PropertyContainer` extends stage C's `NotifyingObject` (the one change
   notification): it overrides `OnPropertyChanged`, the seam left for this stage. The async command
   uses stage A's `Terminator` / `CancellationScope` (timeout on a `TimeProvider`).
2. **Who owns it.** `src/Zerox.Core.Avalonia/ComponentModel` (`PropertyContainer`, internal
   `ThrottlePump`), `Commands` (`ICommandWithInfo`, `Command<T>`, `ObservableTask`,
   `AsyncCommand<T>`, internal `CancelCommand`, internal `UiProgress<T>`), `Dialogs`
   (`MessageKind`, `IDialogService`, `DialogService`, internal `MessageWindow`). Tests mirror them.
3. **Contracts.** `ICommandWithInfo` (menus and toolbars hold commands of both kinds);
   `IDialogService` (view models get it injected; tests substitute it). No base class for
   validation: a view model holds stage C's `Validator`.
4. **Threads.** `MemberChanged` is raised at once on the caller's thread (listeners in the model);
   `PropertyChanged` and `CanExecuteChanged` are posted to the UI thread through the `Dispatcher`,
   explicitly (bindings read there). The throttle pump is one timer per (`TimeProvider`, interval),
   shared by every container with that interval; its tick posts the flush to the UI thread.
5. **Errors (P9).** A synchronous command lets the exception reach its caller. An async command
   run from a binding (`ICommand.Execute`, no caller) is `async void` on the UI thread: an exception
   of the work reaches the dispatcher and stops the application; `ExecuteAsync` gives it to its
   caller. A cancellation (cancel command or timeout) is an expected end, not an error.

## Inventario — capacità e dove esistono già

| Capability (what it does) | Old code | Here / Avalonia | Decision |
|---|---|---|---|
| View model base: immediate member events for the model, UI-thread property events for bindings, events off/on | `PropertyContainer` + `MemberContainer` (+ `IHasGuid` identity by Guid) | `NotifyingObject` (stage C), `Dispatcher.UIThread.Post` | port as `PropertyContainer : NotifyingObject`; identity by Guid dropped (reference identity) |
| Throttled (and debounced) property events, one shared timer per interval | `PropertyContainer.ThrottlePump` on WPF `DispatcherTimer` (+ `ThrotlingTest`, timing-based) | `TimeProvider.CreateTimer` + `Dispatcher.UIThread.Post` | port; tests deterministic on `FakeTimeProvider`; fix: stopping the throttle dropped the pending events (now sent) |
| A property whose value notifies: its inner change is a change of the property | `SetComplexProperty` (+ `NestedPropertyChangedTest`) | — | port; fix: the old child was never unsubscribed (a leak; the old leak test asserted `IsAlive`, i.e. it pinned the leak) |
| Command with label/icon, can-execute, re-query | `Command<T>`, `ICommandWithInfo` (WPF `CommandManager.RequerySuggested`, `Icon` as a MahApps enum, `Tag`) | Avalonia has no `CommandManager` | `Command<T>` + `ICommandWithInfo` (`Label`, `Icon` as a resource key string); re-query from an `INotifyPropertyChanged` source given to the command; `Tag` dropped; a parameter of another type → `ArgumentException` (was silently null) |
| Async command: running state, cancel command, timeout, confirmation | `AsyncCommand<T>`, `CancelAsyncCommand`, `ObservableTask` (catch-all swallowing the failure; `exceptionFunc` callback; `SafeFireAndForget`) | `Terminator`/`CancellationScope` (stage A), `ConfigureAwaitOptions.SuppressThrowing` | ONE `AsyncCommand<T>`; failure → caller or dispatcher (P9), no swallowing, no `exceptionFunc`; confirmation async (`Func<T?, Task<bool>>`, a dialog can answer it) |
| Async command with progress | `ProgressiveAsyncCommand<TProgress, T>` (`Progress<T>` bound to the current context) | — | `AsyncCommand.WithProgress(...)` factory; the handler runs on the UI thread through the `Dispatcher` explicitly |
| Notify / confirm / error message boxes | `Wpf/MessagingContext.cs` (WPF `MessageBox`), `MessageServiceBase` (1234 lines) | Avalonia has no message box | minimal `DialogService` on a window built in code (user decision); the rest of `MessageServiceBase` only when a need appears |
| Auth commands (`AuthCommand`, `AuthAsyncCommand`, `AuthProgressiveAsyncCommand`) | `Commands/Auth*` + `Core/Auth` | — | not ported (the auth subsystem is product code) |
| Commands on actors with allow/deny | `Commands/Actions/ActionsContext.cs` | — | dropped (user decision) |
| Cyclic job scheduler, working loop | `Commands/Scheduler/Job.cs`, `WorkingProcess.cs` | — | stage E (user decision) |
