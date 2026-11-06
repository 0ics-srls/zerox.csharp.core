namespace Zerox.Core.Scheduling;

// The state of a job: Suspended (not in a ring) → Ready ⇄ Running.
public abstract class JobBase : IJob
{
    private readonly Lock _lock = new();
    private JobState _state = JobState.Suspended;

    protected JobBase(string name, JobKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public JobKind Kind { get; }

    public JobState State
    {
        get
        {
            lock (_lock)
                return _state;
        }
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            // Decided here, under the lock: a suspend can arrive from another thread after the caller looked at the state.
            if (_state == JobState.Suspended)
                return false;
            if (_state != JobState.Ready)
                throw new InvalidOperationException($"The job {Name} is {_state}, not Ready");
            _state = JobState.Running;
        }
        try
        {
            return await ExecuteCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_lock)
            {
                if (_state == JobState.Running)
                    _state = JobState.Ready;   // a suspend that arrived during the run is kept
            }
        }
    }

    public void Suspend()
    {
        lock (_lock)
            _state = JobState.Suspended;
    }

    // While running it stays running and is Ready again at the end.
    public void Resume()
    {
        lock (_lock)
        {
            if (_state != JobState.Running)
                _state = JobState.Ready;
        }
    }

    protected abstract Task<bool> ExecuteCoreAsync(CancellationToken cancellationToken);
}
