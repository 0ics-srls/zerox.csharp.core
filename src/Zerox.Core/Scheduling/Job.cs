namespace Zerox.Core.Scheduling;

public sealed class Job : JobBase
{
    private readonly Func<CancellationToken, Task> _action;

    public Job(string name, Func<CancellationToken, Task> action, JobKind kind)
        : base(name, kind)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    public static Job Cyclic(string name, Func<CancellationToken, Task> action) => new(name, action, JobKind.Cyclic);

    public static Job Once(string name, Func<CancellationToken, Task> action) => new(name, action, JobKind.RunOnce);

    protected override async Task<bool> ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        await _action(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
