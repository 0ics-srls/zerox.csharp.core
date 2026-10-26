using System.Diagnostics.CodeAnalysis;
using Zerox.Core.Collections;

namespace Zerox.Core.Scheduling;

// A ring of jobs (a queue is a job too): each execution runs the current job and moves on.
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The name of the scheduler's queues in the old code; it is a ring of jobs, not a Queue<T>.")]
public sealed class JobQueue : JobBase
{
    private readonly Lock _lock = new();
    private readonly CircularLinkedList<IJob> _ring = new();
    private readonly Action<IJob, Exception>? _onJobError;

    // With onJobError the queue is the supervisor of its jobs: a failure is reported and the ring goes on.
    public JobQueue(string name, Action<IJob, Exception>? onJobError = null)
        : base(name, JobKind.Cyclic)
        => _onJobError = onJobError;

    public int Count
    {
        get
        {
            lock (_lock)
                return _ring.Count;
        }
    }

    public bool Contains(IJob job)
    {
        lock (_lock)
            return _ring.Contains(job);
    }

    // The job joins the ring at its end and is resumed.
    public void Add(IJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_lock)
        {
            _ring.Add(job);
            job.Resume();
        }
    }

    // The job leaves the ring and is suspended.
    public void Remove(IJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_lock)
        {
            _ring.Remove(job);
            job.Suspend();
        }
    }

    public void Reset()
    {
        lock (_lock)
            _ring.Reset();
    }

    protected override bool ExecuteCore()
    {
        lock (_lock)
        {
            var job = _ring.Next();
            if (job is null || job.State != JobState.Ready)
                return true;
            var leaves = true;   // a one-time job that ran, or failed, leaves the ring
            try
            {
                leaves = job.Execute();
            }
#pragma warning disable CA1031 // the supervisor (user decision): with a handler, a job's failure is reported and the ring goes on
            catch (Exception error) when (_onJobError is not null)
#pragma warning restore CA1031
            {
                _onJobError(job, error);
            }
            finally
            {
                if (leaves && job.Kind == JobKind.RunOnce)
                {
                    _ring.Remove(job);
                    job.Suspend();
                }
            }
            return true;
        }
    }
}
