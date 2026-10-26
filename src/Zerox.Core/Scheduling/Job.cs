namespace Zerox.Core.Scheduling;

public sealed class Job : JobBase
{
    private readonly Action _action;

    public Job(string name, Action action, JobKind kind)
        : base(name, kind)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    public static Job Cyclic(string name, Action action) => new(name, action, JobKind.Cyclic);

    public static Job Once(string name, Action action) => new(name, action, JobKind.RunOnce);

    protected override bool ExecuteCore()
    {
        _action();
        return true;
    }
}
