using System.ComponentModel;
using System.Windows.Input;
using Zerox.Core.Avalonia.Threading;
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

    // the requery follows the UI thread's rule: at once on it, posted from another thread, on the caller where no application runs
    public void RaiseCanExecuteChanged() => UiThread.Run(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));

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
