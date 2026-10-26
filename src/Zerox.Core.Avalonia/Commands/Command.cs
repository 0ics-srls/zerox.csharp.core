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
