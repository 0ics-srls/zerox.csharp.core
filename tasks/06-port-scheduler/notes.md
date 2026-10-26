# 06-port-scheduler — notes

## Requirement

Stage E of the port of the old Zerox code: the cyclic job scheduler the user wants in the unified
system (2026-10-26: "non mi dispiace avere uno scheduler nel nostro sistema unificato"). It ran for
7 years on 500 machines without a crash. Source (never edited):
`assetforge/tmp/zerox/Zerox.Wpf.Core/Core/Commands/Scheduler/Job.cs` (901 lines) and
`Core/Commands/WorkingProcess.cs`; tests `Zerox.Wpf.XTest.Core/Core/Commands/Scheduler/JobTest.cs`
(1), `JobQueueTest.cs` (7), `JobSchedulerTest.cs` (5).

The model, kept as it is:
- a queue is itself a job (composite); a queue is a ring and each `Execute` runs ONE element of it
  and moves on;
- the scheduler has a main ring and a priority ring; the slow queue (and every custom queue) is an
  element of the main ring: one full lap of the main jobs runs ONE slow job, in turn; a tick runs
  one main element and one priority element;
- one-time jobs leave the ring after running (also when they fail); a delayed one-time job answers
  "not run" until its time and stays;
- groups: suspending a job takes it out of its ring, resuming puts it back;
- jobs are synchronous (the time of a cycle is the sum of its jobs: an `await` would break it);
- `WorkingProcess` is the container that runs the cycles: a loop that runs the cycle, waits the
  rest of the period, keeps the statistics of the cycles.

Decisions taken with the user:
- errors: the scheduler is the SUPERVISOR of its jobs — a job's exception goes, with the job and the
  time, into an errors channel the owner reads (the old code sent it to an event, and to the log
  when nobody listened); the runtime stays alive. This is the explicit P9 exception of this
  component. A failure of the engine itself (the loop, the structure) stops the process (P9);
- the old main/slow/priority structure stays (no redesign).

## Code Organization

1. **Who owns it.** `src/Zerox.Core/Scheduling` (namespace `Zerox.Core.Scheduling`), tests
   `tests/Zerox.Core.Tests/Scheduling`.
2. **Reused.** Stage A's `CircularLinkedList<T>` is the ring of every queue (Next = current, then
   move); stage A's `OwnerQueue.StartDetached` starts the loop of `WorkingProcess` (an exception
   escaping the loop stops the process); `TimeProvider` for the delayed jobs, the error times, the
   period and the statistics (tests on `FakeTimeProvider`, ticks driven by hand).
3. **Contracts.** `IJob` (a ring holds plain jobs and queues alike); `JobBase` (abstract, one level:
   the state Ready/Running/Suspended and the execution guard, shared by `Job`, `DelayedJob`,
   `JobQueue`). `JobScheduler` and `WorkingProcess` are concrete; a runtime is their composition
   (`new WorkingProcess(name, _ => scheduler.Tick(), period, time)`).

## Inventario — capacità e dove esistono già

| Capability (what it does) | Old code | Here / .NET 10 | Decision |
|---|---|---|---|
| A job with a state (ready, running, suspended), executed by a ring | `IJob`, `JobBase` (`MemberContainer`, Guid identity), `Job` (cyclic / run once), `JobKind`, `JobState` | — | port; enum members PascalCase; fix: a suspend that arrived while the job ran was lost (the `finally` set Ready again); `ApplicationException` → `InvalidOperationException` |
| A one-time job that waits for its time | `JobOneTimeDelayed` (`DateTime.Now`) | `TimeProvider` | port as `DelayedJob` on the `TimeProvider` |
| A ring of jobs, one per execution; nested rings | `JobCyclicQueue` on the old circular list (+ `JobQueueTest`, 7) | `CircularLinkedList<T>` (stage A) | port as `JobQueue`; with an error handler (the scheduler's) a failing job is reported and the ring goes on; without one the exception reaches the caller (the old behaviour); a job suspended directly while still in the ring is skipped instead of throwing |
| Main / slow / priority / custom queues, groups, one-time and delayed jobs, suspend/resume by job and group | `JobScheduler`, `JobDescriptor`, `JobGroup(s)`, `JobQueueGroup` (`NamedChildrenContainer`) (+ `JobSchedulerTest`, 5, real time and ±2) | — | port as `JobScheduler` (dictionaries for the indexes); fixes: `CreateCustomJob` put custom jobs in "PriorityGroup" (suspending priority suspended them); an unknown job, group or queue was silently ignored (now `ArgumentException`); a duplicate name; errors to a channel (user decision); tests exact on `Tick()` |
| The loop that runs the cycles, with statistics | `WorkingProcess` (own `Thread`, `WaitOne` of the remaining time, errors to a weak event or the log, `TimeoutException` logged, `canStart`/`canStop`/start/stop procedures), `ExtendedJobScheduler` (scheduler + process, `CyclesDone`) | `PeriodicTimer` on `TimeProvider`, `OwnerQueue.StartDetached` | port as `WorkingProcess(name, cycle, period, time)`: first cycle at once, then one per period (a cycle longer than the period is followed at once), `CyclesDone`, `LastCycle`, `MeanCycle`; an exception escaping the cycle stops the process (the scheduler already reports its jobs' errors); the start/stop hooks dropped (the caller runs them around `Start`/`StopAsync`); `ExtendedJobScheduler` becomes the composition |
