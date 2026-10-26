# Jobs and queues

<block id="01-jobs">
## TDDAB-1: Port the Jobs

<intro>
Delivers, in `src/Zerox.Core/Scheduling`:
- `JobKind` {RunOnce, Cyclic} and `JobState` {Suspended, Ready, Running} (old `RUN_ONCE`/`RUN_CYCLIC`,
  `SUSPEND`/`READY`/`RUNNING`; the unused `UNDEF` dropped);
- `IJob`: `Name`, `Kind`, `State`, `bool Execute()` (false when it had nothing to do yet),
  `Suspend()`, `Resume()` — what a ring holds (plain jobs and queues alike);
- `JobBase` (abstract): a new job is `Suspended` (not in any ring); `Execute()` from `Ready` only
  (otherwise `InvalidOperationException`, was `ApplicationException`), state `Running` during the
  run, back to `Ready` after it — UNLESS it was suspended while running (the old `finally` set
  `Ready` again and the suspend was lost); `Resume()` while running leaves it running (it is Ready
  again at the end); `protected abstract bool ExecuteCore()`; one `Lock` for the state;
- `Job` (sealed): an `Action`, `Job.Cyclic(name, action)` and `Job.Once(name, action)`;
- `DelayedJob` (sealed, `RunOnce`): `DelayedJob(string name, Action action, TimeProvider timeProvider,
  DateTimeOffset dueAt)`; `Execute()` returns false and does nothing until `dueAt`, then runs the
  action and returns true; `DueAt`.

Capability: old `IJob`, `JobBase` (on `MemberContainer`, identity by Guid), `Job`,
`JobOneTimeDelayed` (`DateTime.Now`), `JobKind`, `JobState` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/Scheduler/Job.cs`; test `Test_Job` in
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Commands/Scheduler/JobTest.cs`. Identity by reference
(the Guid equality dropped); the empty `Dispose` pattern dropped (jobs hold nothing to release).

Files: `src/Zerox.Core/Scheduling/JobKind.cs`, `JobState.cs`, `IJob.cs`, `JobBase.cs`, `Job.cs`,
`DelayedJob.cs`; tests `tests/Zerox.Core.Tests/Scheduling/JobTests.cs`.
</intro>

<red>
- test: Test_Job — a cyclic job resumed and executed runs its action (`Kind` Cyclic); a run-once job resumed and executed runs its action (`Kind` RunOnce)
- test: Should_StartSuspended_WhenCreated — a new job is `Suspended`; `Execute()` throws `InvalidOperationException` and the action did not run
- test: Should_StaySuspended_WhenSuspendedWhileRunning — the action suspends its own job: after `Execute()` the state is `Suspended` (the old code put it back to Ready)
- test: Should_BeReadyAgain_WhenResumedWhileRunning — the action calls `Resume()` on its own job: during the run the state is `Running`, after it `Ready`
- test: Should_WaitForItsTime_WhenDelayed — a `DelayedJob` due in 10 s on a fake clock: `Execute()` false and the action not run; after `Advance(10 s)` `Execute()` true and the action ran once
</red>

### Implementation

```csharp
namespace Zerox.Core.Scheduling;

public enum JobKind
{
    RunOnce,
    Cyclic,
}
```

```csharp
namespace Zerox.Core.Scheduling;

public enum JobState
{
    Suspended,
    Ready,
    Running,
}
```

```csharp
namespace Zerox.Core.Scheduling;

// What a ring holds: plain jobs and queues alike.
public interface IJob
{
    string Name { get; }

    JobKind Kind { get; }

    JobState State { get; }

    // False when it had nothing to do yet (a delayed job before its time).
    bool Execute();

    void Suspend();

    void Resume();
}
```

```csharp
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

    public bool Execute()
    {
        lock (_lock)
        {
            if (_state != JobState.Ready)
                throw new InvalidOperationException($"The job {Name} is {_state}, not Ready");
            _state = JobState.Running;
        }
        try
        {
            return ExecuteCore();
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

    protected abstract bool ExecuteCore();
}
```

```csharp
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
```

```csharp
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
```

<success>
- [ ] `JobTests` pass (5 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="02-job-queue">
## TDDAB-2: Port the Job Queue Ring

<intro>
Delivers `JobQueue` (sealed, `JobBase`, `Cyclic`), the ring on stage A's `CircularLinkedList<IJob>`:
- `JobQueue(string name, Action<IJob, Exception>? onJobError = null)`;
- `Add(IJob job)`: the job joins the ring at its end and is resumed (`Ready`); a job already in the
  ring → `ArgumentException`; `Remove(IJob job)`: leaves the ring and is suspended; `Contains`,
  `Count`, `Reset()` (the cursor back to the first job); one `Lock` for the ring, held during an
  execution (a remove from another thread waits for the running job);
- `Execute()` (through `JobBase`) runs ONE element: the current job (`Next()`), then the cursor
  moves; an empty ring does nothing; a job that is not `Ready` (suspended directly while still in
  the ring) is skipped — the old code threw; a `RunOnce` job that ran (true) — or failed — leaves
  the ring and is suspended; a job that answered false (a delayed job before its time) stays;
- errors: WITH `onJobError` the queue is the supervisor — a job's exception is handed to the
  handler with the job, and the ring goes on; WITHOUT it the exception reaches the caller (the old
  behaviour); a nested queue (a queue is a job) with its own handler reports its own jobs.

Capability: old `JobCyclicQueue` / `IJobsQueue` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/Scheduler/Job.cs`; tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Commands/Scheduler/JobQueueTest.cs` (7, ported with the
same numbers; `Size` → `Count`; `Job.CreateCyclic`/`CreateRunOnce` → `Job.Cyclic`/`Job.Once`).

Files: `src/Zerox.Core/Scheduling/JobQueue.cs`; tests `tests/Zerox.Core.Tests/Scheduling/JobQueueTests.cs`.
</intro>

<red>
- test: Test_JobQueue_run_once — 5 run-once jobs adding 1..5: after 5 executions `cnt` 15, `Count` 0, none contained, all `Suspended`
- test: Test_JobQueue_run_cyclic — 5 cyclic jobs: after 5 executions `cnt` 15 and `Count` 5; after 5 more `cnt` 30; all contained and `Ready`
- test: Test_JobQueue_run_mixed — 4 cyclic + 1 run-once: after 5 executions `cnt` 15, `Count` 4, j5 out; after 5 more `cnt` 26 (j1 runs twice); j5 `Suspended`, the others `Ready`
- test: Test_JobQueue_with_child_queue — main (j1, j2, j3, child (j4, j5)): 2 × `Count` executions → `cnt` 21; j2 removed from main and j4 from child: 2 × `Count` executions → `cnt` 18; j2 and j4 `Suspended`, the child and the others `Ready`
- test: Test_JobQueue_with_child_error_queue — main (j1, j2, j3 cyclic, child (j4, j5 run-once)): 3 × `Count` executions → `cnt` 27, child `Count` 0
- test: Test_JobQueue_with_many_jobs — 10 000 cyclic jobs adding their index: 10 000 executions → the sum of 0..9999, `Count` 10 000
- test: Test_JobQueue_with_child_queue_sequence — the order of 2 laps is ",j1,j2,j3,j4,j1,j2,j3,j5"
- test: Should_ReportTheFailureAndGoOn_WhenAnErrorHandlerIsGiven — a failing cyclic job and a failing run-once job with a handler: no exception from `Execute()`; the handler got both with their exceptions; the cyclic job stays, the run-once job left
- test: Should_ThrowAndRemoveTheOneTimeJob_WhenThereIsNoErrorHandler — without a handler: `Execute()` throws the job's exception and the failing run-once job is out of the ring
- test: Should_SkipAJob_WhenItWasSuspendedInTheRing — a job suspended directly (still contained): the execution that reaches it does nothing and does not throw; after `Resume()` it runs
- test: Should_RejectAJob_WhenItIsAlreadyInTheRing — adding the same job twice throws `ArgumentException`
</red>

### Implementation

```csharp
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
```

<success>
- [ ] `JobQueueTests` pass (11 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
