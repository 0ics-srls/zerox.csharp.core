namespace Zerox.Core.Scheduling;

// A one-time job that does nothing until its time.
public sealed class DelayedJob : JobBase
{
    private readonly Func<CancellationToken, Task> _action;
    private readonly TimeProvider _time;

    public DelayedJob(string name, Func<CancellationToken, Task> action, TimeProvider timeProvider, DateTimeOffset dueAt)
        : base(name, JobKind.RunOnce)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _action = action;
        _time = timeProvider;
        DueAt = dueAt;
    }

    public DateTimeOffset DueAt { get; }

    protected override async Task<bool> ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (_time.GetUtcNow() < DueAt)
            return false;
        await _action(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
