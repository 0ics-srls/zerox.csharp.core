namespace Zerox.Core.Scheduling;

// A one-time job that does nothing until its time.
public sealed class DelayedJob : JobBase
{
    private readonly Action _action;
    private readonly TimeProvider _time;

    public DelayedJob(string name, Action action, TimeProvider timeProvider, DateTimeOffset dueAt)
        : base(name, JobKind.RunOnce)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _action = action;
        _time = timeProvider;
        DueAt = dueAt;
    }

    public DateTimeOffset DueAt { get; }

    protected override bool ExecuteCore()
    {
        if (_time.GetUtcNow() < DueAt)
            return false;
        _action();
        return true;
    }
}
