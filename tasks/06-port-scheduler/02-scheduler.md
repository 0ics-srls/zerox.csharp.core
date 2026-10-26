# Scheduler and loop

<block id="03-job-scheduler">
## TDDAB-3: Port the Job Scheduler

<intro>
Delivers, in `src/Zerox.Core/Scheduling`:
- `JobError` (sealed record): `JobName`, `Exception`, `At` (`DateTimeOffset`, from the `TimeProvider`);
- `JobScheduler` (sealed): `JobScheduler(string name, TimeProvider timeProvider)`; constants
  `MainQueue`, `SlowQueue`, `PriorityQueue` and groups `MainGroup`, `SlowGroup`, `PriorityGroup`;
  the main and priority rings; the slow queue is created in the constructor and is the FIRST
  element of the main ring (as in the old code); every queue the scheduler creates reports its
  jobs' failures to the `Errors` channel (`ChannelReader<JobError>`, unbounded: the owner reads it);
  - `Tick()`: one element of the main ring, then one of the priority ring (a job's failure is in
    the channel, so the priority element runs also when a main job fails — the old code skipped
    it); nothing while the scheduler is suspended;
  - cyclic jobs (indexed by name; a duplicate name → `ArgumentException`):
    `CreateMainJob(name, action, params string[] groups)` (+ `MainGroup`),
    `CreateSlowJob(...)` (+ `SlowGroup`), `CreatePriorityJob(...)` (+ `PriorityGroup`),
    `CreateCustomJob(name, queueName, action, params string[] groups)` (only the given groups — the
    old code added `PriorityGroup`, so suspending priority suspended custom jobs),
    `CreateJobQueue(name, params string[] groups)` (a new queue, element of the main ring, + `MainGroup`),
    `AddJob(IJob job, string queueName, params string[] groups)` (any `IJob`; a run-once job is not
    indexed: it leaves after running);
  - one-time jobs: `CreateMainOneTimeJob(name, action)`, `CreatePriorityOneTimeJob`,
    `CreateCustomOneTimeJob(name, queueName, action)`; delayed: `CreateMainOneTimeDelayedJob(name,
    action, DateTimeOffset when)` and `(name, action, TimeSpan after)` (due = now + after), the same
    for priority and custom;
  - `SuspendJob(name)` / `ResumeJob(name)`, `SuspendGroup(name)` / `ResumeGroup(name)` (a job is
    taken out of / put back at the end of its ring; resuming a job still in its ring makes it Ready);
    an unknown job, group or queue → `ArgumentException` (the old code ignored it);
  - `Suspend()` / `Resume()` / `IsSuspended` for the whole scheduler; `Reset()` (every cursor back
    to the start).
  One `Lock` for the indexes; the rings have their own.

Capability: old `IJobScheduler`, `JobScheduler`, `JobDescriptor`, `JobGroup`, `JobGroups`,
`JobQueueGroup`, `ExtendedJobScheduler` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/Scheduler/Job.cs`; tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Commands/Scheduler/JobSchedulerTest.cs` (5: real time
500 ms and counts within ±2 — ported EXACT by calling `Tick()`; with the slow queue first in the
main ring and jobs JM_1, JM_2 (main), JS_1, JS_2 (slow), JE_1, JE_2 (priority), 600 ticks give
JM 200 each, JS 100 each, JE 300 each). The runner of `ExtendedJobScheduler` is block 04
(`WorkingProcess` running `Tick()`).

Files: `src/Zerox.Core/Scheduling/JobError.cs`, `JobScheduler.cs`; tests
`tests/Zerox.Core.Tests/Scheduling/JobSchedulerTests.cs`.
</intro>

<red>
- test: Test_Create_Scheduler — a new scheduler has the three queues: `CreateMainJob`, `CreateSlowJob`, `CreatePriorityJob` work and a tick runs
- test: Test_Main_And_Slow_Running — the old jobs, 600 ticks: JM_1 = JM_2 = 200, JS_1 = JS_2 = 100, JE_1 = JE_2 = 300
- test: Test_Main_And_Slow_And_custom_Running — plus `CreateJobQueue("CustomQueue")` and JC_1, JC_2 in it, 800 ticks: JM 200 each, JS 100 each, JC 100 each, JE 400 each
- test: Test_Main_And_Slow_suspended — groups "MainSuspend" and "PrioritySuspend" suspended, 600 ticks: JM_1 300, JM_2 0, JS 150 each, JE_1 600, JE_2 0; then both resumed, `Reset()`, counters cleared, 600 ticks: the counts of `Test_Main_And_Slow_Running`
- test: Test_Multithread_Suspend_And_Resume @Integration — a thread ticks (at least 200 000 times and until the 50 one-time jobs ran; bounded at 10 000 000) while one thread toggles "MainSuspend" 1000 times, one toggles "SlowSuspend" 1000 times, one adds 50 priority one-time jobs: no error in the channel, the 50 one-time jobs ran, JM_1 ≥ JM_2, JS_1 ≥ JS_2
- test: Should_KeepCustomJobsRunning_WhenThePriorityGroupIsSuspended — `SuspendGroup(PriorityGroup)` does not stop a custom job (the old code did)
- test: Should_ReportTheFailureAndRunThePriority_WhenAMainJobFails — a failing main job "F" (main ring: the slow queue, then F) and a priority job: 2 ticks do not throw, the priority job ran twice, the channel has one `JobError` with "F", its exception and the fake clock's time; 2 more ticks → a second error (the failing cyclic job stays in its ring)
- test: Should_RunADelayedJobOnce_WhenItsTimeComes — `CreatePriorityOneTimeDelayedJob(name, action, 5 s)`: ticks before the time do not run it; after `Advance(5 s)` the next tick runs it once and later ticks do not
- test: Should_RejectAName_WhenItIsTakenOrUnknown — a second job with the same name → `ArgumentException`; `SuspendJob`, `ResumeGroup`, `CreateCustomJob` with an unknown name → `ArgumentException`
- test: Should_DoNothing_WhenTheSchedulerIsSuspended — `Suspend()`: ticks run no job; `Resume()`: they run again
</red>

### Implementation

```csharp
namespace Zerox.Core.Scheduling;

// A job's failure, reported by its queue (the scheduler is the supervisor of its jobs).
public sealed record JobError(string JobName, Exception Exception, DateTimeOffset At);
```

```csharp
using System.Threading.Channels;

namespace Zerox.Core.Scheduling;

// Main ring (main jobs + slow queue + custom queues) and priority ring; a tick runs one element of each.
public sealed class JobScheduler
{
    public const string MainQueue = "MainQueue";
    public const string SlowQueue = "SlowQueue";
    public const string PriorityQueue = "PriorityQueue";
    public const string MainGroup = "MainGroup";
    public const string SlowGroup = "SlowGroup";
    public const string PriorityGroup = "PriorityGroup";

    private readonly Lock _lock = new();
    private readonly TimeProvider _time;
    private readonly Channel<JobError> _errors = Channel.CreateUnbounded<JobError>();
    private readonly JobQueue _main;
    private readonly JobQueue _priority;
    private readonly Dictionary<string, JobQueue> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (IJob Job, JobQueue Queue)> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(IJob Job, JobQueue Queue)>> _groups = new(StringComparer.Ordinal);

    public JobScheduler(string name, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Name = name;
        _time = timeProvider;
        _main = NewQueue(MainQueue);
        _priority = NewQueue(PriorityQueue);
        _main.Resume();
        _priority.Resume();
        _queues[MainQueue] = _main;
        _queues[PriorityQueue] = _priority;
        CreateJobQueue(SlowQueue);
    }

    public string Name { get; }

    public ChannelReader<JobError> Errors => _errors.Reader;

    public bool IsSuspended => _main.State == JobState.Suspended;

    // One element of the main ring, then one of the priority ring.
    public void Tick()
    {
        if (_main.State == JobState.Ready)
            _main.Execute();
        if (_priority.State == JobState.Ready)
            _priority.Execute();
    }

    public void Suspend()
    {
        _main.Suspend();
        _priority.Suspend();
    }

    public void Resume()
    {
        _main.Resume();
        _priority.Resume();
    }

    public void Reset()
    {
        lock (_lock)
        {
            foreach (var queue in _queues.Values)
                queue.Reset();
        }
    }

    public void CreateJobQueue(string name, params string[] groups)
    {
        var queue = NewQueue(name);
        lock (_lock)
        {
            if (!_queues.TryAdd(name, queue))
                throw new ArgumentException($"A queue named {name} exists", nameof(name));
        }
        AddJob(queue, MainQueue, [.. groups, MainGroup]);
    }

    public void CreateMainJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), MainQueue, [.. groups, MainGroup]);

    public void CreateSlowJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), SlowQueue, [.. groups, SlowGroup]);

    public void CreatePriorityJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), PriorityQueue, [.. groups, PriorityGroup]);

    public void CreateCustomJob(string name, string queueName, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), queueName, groups);

    public void CreateMainOneTimeJob(string name, Action action) => AddJob(Job.Once(name, action), MainQueue);

    public void CreatePriorityOneTimeJob(string name, Action action) => AddJob(Job.Once(name, action), PriorityQueue);

    public void CreateCustomOneTimeJob(string name, string queueName, Action action) => AddJob(Job.Once(name, action), queueName);

    public void CreateMainOneTimeDelayedJob(string name, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), MainQueue);

    public void CreateMainOneTimeDelayedJob(string name, Action action, TimeSpan after) => CreateMainOneTimeDelayedJob(name, action, _time.GetUtcNow() + after);

    public void CreatePriorityOneTimeDelayedJob(string name, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), PriorityQueue);

    public void CreatePriorityOneTimeDelayedJob(string name, Action action, TimeSpan after) => CreatePriorityOneTimeDelayedJob(name, action, _time.GetUtcNow() + after);

    public void CreateCustomOneTimeDelayedJob(string name, string queueName, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), queueName);

    public void CreateCustomOneTimeDelayedJob(string name, string queueName, Action action, TimeSpan after) => CreateCustomOneTimeDelayedJob(name, queueName, action, _time.GetUtcNow() + after);

    // Any job into a queue; a cyclic one is indexed by name and joins the groups, a one-time one leaves after running.
    public void AddJob(IJob job, string queueName, params string[] groups)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(groups);
        lock (_lock)
        {
            var queue = Queue(queueName);
            if (job.Kind == JobKind.Cyclic)
            {
                if (!_jobs.TryAdd(job.Name, (job, queue)))
                    throw new ArgumentException($"A job named {job.Name} exists", nameof(job));
                foreach (var group in groups)
                {
                    if (!_groups.TryGetValue(group, out var members))
                        _groups[group] = members = [];
                    members.Add((job, queue));
                }
            }
            queue.Add(job);
        }
    }

    public void SuspendJob(string name)
    {
        lock (_lock)
            Suspend(Indexed(name));
    }

    public void ResumeJob(string name)
    {
        lock (_lock)
            Resume(Indexed(name));
    }

    public void SuspendGroup(string name)
    {
        lock (_lock)
        {
            foreach (var member in Group(name))
                Suspend(member);
        }
    }

    public void ResumeGroup(string name)
    {
        lock (_lock)
        {
            foreach (var member in Group(name))
                Resume(member);
        }
    }

    private static void Suspend((IJob Job, JobQueue Queue) member) => member.Queue.Remove(member.Job);

    private static void Resume((IJob Job, JobQueue Queue) member)
    {
        if (!member.Queue.Contains(member.Job))
            member.Queue.Add(member.Job);
        else
            member.Job.Resume();
    }

    private JobQueue NewQueue(string name)
        => new(name, (job, error) => _errors.Writer.TryWrite(new JobError(job.Name, error, _time.GetUtcNow())));

    private JobQueue Queue(string name)
        => _queues.TryGetValue(name, out var queue) ? queue : throw new ArgumentException($"No queue named {name}", nameof(name));

    private (IJob Job, JobQueue Queue) Indexed(string name)
        => _jobs.TryGetValue(name, out var member) ? member : throw new ArgumentException($"No job named {name}", nameof(name));

    private List<(IJob Job, JobQueue Queue)> Group(string name)
        => _groups.TryGetValue(name, out var members) ? members : throw new ArgumentException($"No group named {name}", nameof(name));
}
```

Test shape (the old `Worker` keeps its counters; the ported tests tick by hand):

```csharp
private static JobScheduler WithOldJobs(Worker w, TimeProvider? time = null)
{
    var sch = new JobScheduler("Scheduler", time ?? new FakeTimeProvider());
    sch.CreateMainJob("JM_1", w.Add_M_1, "Main");
    sch.CreateMainJob("JM_2", w.Add_M_2, "MainSuspend");
    sch.CreateSlowJob("JS_1", w.Add_S_1, "Slow");
    sch.CreateSlowJob("JS_2", w.Add_S_2, "SlowSuspend");
    sch.CreatePriorityJob("JE_1", w.Add_E_1, "Priority");
    sch.CreatePriorityJob("JE_2", w.Add_E_2, "PrioritySuspend");
    return sch;
}

[Fact]
public void Test_Main_And_Slow_Running()
{
    var w = new Worker();
    var sch = WithOldJobs(w);

    for (var i = 0; i < 600; i++)
        sch.Tick();

    // main ring: SlowQueue, JM_1, JM_2 → a lap of 3 ticks runs one slow job
    Assert.Equal(200, w.JM_1_cnt);
    Assert.Equal(200, w.JM_2_cnt);
    Assert.Equal(100, w.JS_1_cnt);
    Assert.Equal(100, w.JS_2_cnt);
    Assert.Equal(300, w.JE_1_cnt);
    Assert.Equal(300, w.JE_2_cnt);
}
```

<success>
- [ ] `JobSchedulerTests` pass (10 tests, the ported ones with exact counts)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="04-working-process">
## TDDAB-4: Port the Working Process That Runs the Cycles

<intro>
Delivers `WorkingProcess` (sealed, `IAsyncDisposable`, `src/Zerox.Core/Scheduling/WorkingProcess.cs`),
the container that runs a cycle periodically — the runtime of a scheduler is
`new WorkingProcess(name, _ => scheduler.Tick(), period, time)`:
- `WorkingProcess(string name, Action<CancellationToken> cycle, TimeSpan period, TimeProvider timeProvider)`
  (`period` ≤ 0 → `ArgumentOutOfRangeException`); `Name`, `Period`, `IsActive`;
- `Start()`: the first cycle runs at once, then one cycle per period (`PeriodicTimer(period,
  timeProvider)` created inside `Start`, so a fake clock advanced right after `Start` already ticks
  it); a cycle longer than the period is followed at once (the old "wait only the rest");
  the loop runs through stage A's `OwnerQueue.StartDetached`: an exception escaping the cycle stops
  the process (P9 — the scheduler already reports its jobs' failures, so what escapes is the
  engine's own failure); started twice → `InvalidOperationException`; the cycle gets the stop token;
- `StopAsync()`: cancels the token, disposes the timer (the pending wait returns false) and waits
  for the loop's end; safe when not started; `Start` again after a stop is allowed (statistics
  restart); `DisposeAsync()` = `StopAsync()`;
- statistics (on the `TimeProvider`'s timestamps): `CyclesDone`, `LastCycle`, `MeanCycle`
  (`TimeSpan.Zero` before any cycle).

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Commands/WorkingProcess.cs` (own `Thread`,
`WaitOne` of the remaining time, `CancellationContext` from the old terminator, errors to a weak
event or the log, `TimeoutException` only logged, `canStart`/`canStop`/start/stop procedures,
`IoCEnabledPropertyContainer` + Autofac factory) and `ExtendedJobScheduler` (`Start`/`Stop`,
`CyclesDone`). Dropped: the start/stop hooks (the caller runs them around `Start`/`StopAsync`),
the error event (the scheduler's errors channel reports the jobs; the rest stops the process). No
old tests (the scheduler tests used it with real time).

Files: `src/Zerox.Core/Scheduling/WorkingProcess.cs`; tests
`tests/Zerox.Core.Tests/Scheduling/WorkingProcessTests.cs` (fake clock; each cycle writes to a
`Channel` the test reads with `TestContext.Current.CancellationToken`; no sleep).
</intro>

<red>
- test: Should_RunTheFirstCycleAtOnce_WhenStarted — `Start()`: one cycle runs without advancing the clock; `IsActive` true
- test: Should_RunACyclePerPeriod_WhenTheClockAdvances — period 100 ms: after the first cycle, each `Advance(100 ms)` gives one more cycle (3 cycles read); after `StopAsync()` `CyclesDone` is 3
- test: Should_StopCycling_WhenStopped — after `StopAsync()` `IsActive` false; `Advance(1 s)` gives no cycle; `Start()` again runs a first cycle at once
- test: Should_MeasureTheCycles_WhenTheyRun — each cycle advances the fake clock by 30 ms: after 2 cycles and the stop, `LastCycle` and `MeanCycle` are 30 ms
- test: Should_RunTheScheduler_WhenItIsTheCycle — a `JobScheduler` with main jobs A, B and priority job P, the process running `Tick()` with period 10 ms: after 6 cycles A = 2, B = 2 (the slow queue is the first main element), P = 6
- test: Should_ThrowInvalidOperation_WhenStartedTwice — the second `Start()` throws `InvalidOperationException`
- test: Should_RejectThePeriod_WhenItIsNotPositive — `TimeSpan.Zero` → `ArgumentOutOfRangeException`
</red>

### Implementation

```csharp
using Zerox.Core.Concurrency;

namespace Zerox.Core.Scheduling;

// Runs a cycle at once and then once per period; an exception escaping the cycle stops the process.
public sealed class WorkingProcess : IAsyncDisposable
{
    private readonly Action<CancellationToken> _cycle;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _stop;
    private Task _stopped = Task.CompletedTask;
    private long _cycles;
    private long _lastTicks;
    private long _totalTicks;

    public WorkingProcess(string name, Action<CancellationToken> cycle, TimeSpan period, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Name = name;
        Period = period;
        _cycle = cycle;
        _time = timeProvider;
    }

    public string Name { get; }

    public TimeSpan Period { get; }

    public bool IsActive
    {
        get
        {
            lock (_lock)
                return _timer is not null;
        }
    }

    public long CyclesDone => Interlocked.Read(ref _cycles);

    public TimeSpan LastCycle => TimeSpan.FromTicks(Interlocked.Read(ref _lastTicks));

    public TimeSpan MeanCycle
    {
        get
        {
            var cycles = CyclesDone;
            return cycles == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Interlocked.Read(ref _totalTicks) / cycles);
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_timer is not null)
                throw new InvalidOperationException($"{Name} is already running");
            Interlocked.Exchange(ref _cycles, 0);
            Interlocked.Exchange(ref _lastTicks, 0);
            Interlocked.Exchange(ref _totalTicks, 0);
            var timer = new PeriodicTimer(Period, _time);
            var stop = new CancellationTokenSource();
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _timer = timer;
            _stop = stop;
            _stopped = stopped.Task;
            OwnerQueue.StartDetached(() => LoopAsync(timer, stopped, stop.Token));
        }
    }

    public async Task StopAsync()
    {
        PeriodicTimer? timer;
        CancellationTokenSource? stop;
        Task stopped;
        lock (_lock)
        {
            timer = _timer;
            stop = _stop;
            stopped = _stopped;
            _timer = null;
            _stop = null;
        }
        if (timer is null || stop is null)
            return;
        await stop.CancelAsync().ConfigureAwait(false);
        timer.Dispose();   // the pending wait returns false: the loop ends
        await stopped.ConfigureAwait(false);
        stop.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task LoopAsync(PeriodicTimer timer, TaskCompletionSource stopped, CancellationToken stop)
    {
        try
        {
            do
            {
                if (stop.IsCancellationRequested)
                    break;
                var start = _time.GetTimestamp();
                _cycle(stop);
                var elapsed = _time.GetElapsedTime(start);
                Interlocked.Exchange(ref _lastTicks, elapsed.Ticks);
                Interlocked.Add(ref _totalTicks, elapsed.Ticks);
                Interlocked.Increment(ref _cycles);
            }
            while (await timer.WaitForNextTickAsync(CancellationToken.None).ConfigureAwait(false));   // the stop disposes the timer: the wait returns false
        }
        finally
        {
            stopped.SetResult();
        }
    }
}
```

<success>
- [ ] `WorkingProcessTests` pass (7 tests), no sleep
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
