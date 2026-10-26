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
