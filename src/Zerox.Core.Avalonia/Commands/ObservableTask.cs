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
