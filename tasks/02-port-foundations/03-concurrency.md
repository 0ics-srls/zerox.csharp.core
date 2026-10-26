# Concurrency and cancellation

<block id="05-atomic-boolean">
## TDDAB-5: Port the Atomic Boolean

<intro>
Delivers `AtomicBoolean`: a boolean changed with compare-and-set (`FalseToTrue`, `TrueToFalse`,
`SetWhen`) and an optional callback run when the value really changes. Typical use: "start once",
"dispose once".

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Atomic/AtomicBoolean.cs` (test
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Atomic/AtomicBooleanTests.cs`, NUnit). `Interlocked` has
no `bool` overload in .NET 10 that this needs, so the type stays, on an `int`. Bugs fixed: the
callback ran after a successful `SetWhen(x, x)` that changed nothing; the `Value` setter read and
wrote without an atomic exchange; the docs of `FalseToTrue` / `TrueToFalse` were swapped.

Files: `src/Zerox.Core/Concurrency/AtomicBoolean.cs`; tests
`tests/Zerox.Core.Tests/Concurrency/AtomicBooleanTests.cs`.
</intro>

<red>
- test: TestAtomicBoolean — the old test: default false; `new AtomicBoolean(true).Value` true; `FalseToTrue` fails when already true; `TrueToFalse` fails when already false; a failing `SetWhen` leaves the value unchanged
- test: Should_RunTheCallbackOnce_WhenTheValueChanges — `Value = true` from false runs the callback once; `Value = true` again does not run it
- test: Should_NotRunTheCallback_WhenSetWhenChangesNothing — `SetWhen(true, true)` on true returns true and does not run the callback
- test: Should_LetExactlyOneCallerWin_WhenManyCallFalseToTrueTogether — 1000 parallel `FalseToTrue` calls: exactly one returns true
</red>

### Implementation

```csharp
namespace Zerox.Core.Concurrency;

// A boolean changed atomically; the callback runs only when the value really changes.
public sealed class AtomicBoolean(bool initialValue = false, Action? changed = null)
{
    private int _value = initialValue ? 1 : 0;

    public bool Value
    {
        get => Volatile.Read(ref _value) == 1;
        set
        {
            var next = value ? 1 : 0;
            if (Interlocked.Exchange(ref _value, next) != next)
                changed?.Invoke();
        }
    }

    // Changes false to true; true when this call made the change.
    public bool FalseToTrue() => SetWhen(true, false);

    // Changes true to false; true when this call made the change.
    public bool TrueToFalse() => SetWhen(false, true);

    // Sets setTo when the value is when; true when the value was when.
    public bool SetWhen(bool setTo, bool when)
    {
        int expected = when ? 1 : 0, next = setTo ? 1 : 0;
        var matched = Interlocked.CompareExchange(ref _value, next, expected) == expected;
        if (matched && next != expected)
            changed?.Invoke();
        return matched;
    }
}
```

<success>
- [ ] `AtomicBooleanTests` pass (4 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="06-critical-section">
## TDDAB-6: Port the Critical Section That Holds Across Await

<intro>
Delivers `CriticalSection`: mutual exclusion over `SemaphoreSlim(1, 1)`, which (unlike `lock` /
`Monitor`) is not bound to a thread and therefore holds across `await`. The lock is a disposable
handle `CriticalSectionLock` with `IsLocked`.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Async/CriticalSection.cs` (tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Async/CriticalSectionTest.cs`, NUnit, base class with
Autofac). The old `TimedLock` (`Monitor` with timeout) is dropped: the old test
`Test_LockAsyncProblemAsync` proves it breaks across `await`. New: `LockAsync` and `TryLockAsync`,
so a waiter does not block a thread. `Lock(timeout)` throws `TimeoutException` (expected
outcome of a timed wait, as before); `TryLock` / `TryLockAsync` return a handle with
`IsLocked == false` instead. The handle is a top-level type (not nested in `CriticalSection`).

Files: `src/Zerox.Core/Concurrency/CriticalSection.cs` (both types); tests
`tests/Zerox.Core.Tests/Concurrency/CriticalSectionTests.cs`. The two old thread tests depend on
timing: they carry `[Trait("Category", "Integration")]`. Read the old test file for their bodies
(`using (cs.Lock()) { ... }` around `cnts.Work`).
</intro>

<red>
- test: Test_CriticalSection — the old test: two workers doing 100 increments each with an optimistic check inside `using (cs.Lock())` record 0 wrong and 200 good (Integration)
- test: Test_No_CriticalSection — the old test: 10 workers without the lock record at least one wrong (Integration)
- test: Test_CriticalSectionAsyncCompatibilityAsync — the old test: an `await Task.Delay(100)` inside `using (cs.Lock())` throws nothing
- test: Should_RunOneBodyAtATime_WhenFiftyTasksLockAsync — 50 tasks each `using (await cs.LockAsync())` with awaits inside never overlap (maximum concurrency 1) and all complete
- test: Should_ReturnAnUnlockedHandle_WhenTryLockFindsItHeld — while held, `TryLock(TimeSpan.Zero).IsLocked` and `(await TryLockAsync(TimeSpan.Zero)).IsLocked` are false
- test: Should_ThrowTimeout_WhenLockWithTimeoutFindsItHeld — while held, `Lock(TimeSpan.FromMilliseconds(10))` and `LockAsync(TimeSpan.FromMilliseconds(10))` throw `TimeoutException`
- test: Should_ThrowOperationCanceled_WhenLockAsyncIsCanceled — while held, `LockAsync(canceledToken)` throws `OperationCanceledException`
- test: Should_ReleaseOnce_WhenTheHandleIsDisposed — after disposing a held handle the next `TryLock(TimeSpan.Zero)` succeeds; an unlocked handle's `Dispose` releases nothing
</red>

### Implementation

```csharp
namespace Zerox.Core.Concurrency;

// Mutual exclusion that holds across await (SemaphoreSlim is not bound to a thread, unlike lock/Monitor).
public sealed class CriticalSection : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CriticalSectionLock Lock()
    {
        _gate.Wait();
        return new CriticalSectionLock(_gate);
    }

    public CriticalSectionLock Lock(TimeSpan timeout)
        => _gate.Wait(timeout) ? new CriticalSectionLock(_gate) : throw new TimeoutException($"Critical section not acquired in {timeout}");

    public CriticalSectionLock TryLock(TimeSpan timeout)
        => _gate.Wait(timeout) ? new CriticalSectionLock(_gate) : default;

    public async ValueTask<CriticalSectionLock> LockAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new CriticalSectionLock(_gate);
    }

    public async ValueTask<CriticalSectionLock> LockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => await _gate.WaitAsync(timeout, cancellationToken).ConfigureAwait(false)
            ? new CriticalSectionLock(_gate)
            : throw new TimeoutException($"Critical section not acquired in {timeout}");

    public async ValueTask<CriticalSectionLock> TryLockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => await _gate.WaitAsync(timeout, cancellationToken).ConfigureAwait(false) ? new CriticalSectionLock(_gate) : default;

    public void Dispose() => _gate.Dispose();
}

// The held lock; disposing it releases the section. default = not acquired (IsLocked false).
public readonly struct CriticalSectionLock : IDisposable, IEquatable<CriticalSectionLock>
{
    private readonly SemaphoreSlim? _gate;

    internal CriticalSectionLock(SemaphoreSlim gate) => _gate = gate;

    public bool IsLocked => _gate is not null;

    public void Dispose() => _gate?.Release();

    // equality members as CA1815 requires (by the gate reference)
}
```

<success>
- [ ] `CriticalSectionTests` pass (8 tests; the two Integration tests pass when run with `--filter-trait "Category=Integration"` too)
- [ ] No `TimedLock` and no `Monitor` lock helper in `src`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="07-cancellation-scope">
## TDDAB-7: Rewrite the Cancellation Tree on TimeProvider

<intro>
Delivers `Terminator` (the root, one per application: cancels every scope at shutdown) and
`CancellationScope` (a node: a token, an optional timeout, children, a state that says whether it
was canceled or timed out). Built on linked `CancellationTokenSource`s and
`new CancellationTokenSource(timeout, timeProvider)`, so tests drive time with `FakeTimeProvider`.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/CancelContext/*` (tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/CancelContext/TerminatorTest.cs`, which wait real
seconds). Rewritten small; dropped: the logger taken from the Autofac `BootStrapper`, the
`Dispose` loop that retried with `Thread.Sleep` for 5 s, `Cancel()` catching and logging the
callbacks' exceptions (P9: they reach the caller), `WaitUntil` (polling every 100 ms), the
`Timeout()` variant whose disposal also disposed its parent. Kept: the tree, the
state (`Active`, `Canceled`, `TimedOut`), `ThrowIfCanceled`, a cancelable delay, the count of
live scopes.

State rule (as the old one): a scope whose parent (or terminator) is canceled is `Canceled`; else
a scope whose own timeout elapsed is `TimedOut`; else a scope canceled by `Cancel()` is
`Canceled`; else `Active`.

This block also adds the package `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 to
`Directory.Packages.props` and references it from `tests/Zerox.Core.Tests/Zerox.Core.Tests.csproj`.

Files: `src/Zerox.Core/Cancellation/CancellationState.cs`, `src/Zerox.Core/Cancellation/CancellationScope.cs`
(both classes), tests `tests/Zerox.Core.Tests/Cancellation/CancellationScopeTests.cs`.
</intro>

<red>
- test: Should_StayActive_WhenTheWorkEndsBeforeTimeoutAndCancel — a scope with a 3 s timeout; a `DelayAsync(1 s)` completes when the fake clock advances 1 s; the state is `Active`
- test: Should_BeCanceled_WhenCanceledExplicitly — `scope.Cancel()` while a `DelayAsync(10 s)` runs: the delay throws `OperationCanceledException`, the state is `Canceled`
- test: Should_BeTimedOut_WhenTheTimeoutElapses — a scope with a 3 s timeout and a `DelayAsync(10 s)`: advancing 3 s makes the delay throw `OperationCanceledException` and the state `TimedOut`
- test: Should_CancelEveryScope_WhenTheTerminatorCancels — scopes `a`, `b` and children of `a` with timeouts 10 s and 0.5 s: advancing 0.5 s makes the 0.5 s child `TimedOut`; after `terminator.CancelAll()` all four are `Canceled` (the parent rule comes first) and their tokens are canceled
- test: Should_BeCanceled_WhenTheParentIsCanceled — a child of a canceled scope is `Canceled` and its token is canceled
- test: Should_ForgetTheScope_WhenDisposed — `terminator.Count` goes 0 → 2 → 0 as two scopes are created and disposed; a parent's `Count` follows its children the same way
- test: Should_DisposeTheChildren_WhenTheParentIsDisposed — after disposing a parent, its children's `Count` is 0 and creating a child of it throws `ObjectDisposedException`
- test: Should_Throw_WhenThrowIfCanceledAfterCancel — `ThrowIfCanceled()` throws `OperationCanceledException` after `Cancel()`, nothing before
- test: Should_LetTheCallbackExceptionReachTheCaller_WhenACancellationCallbackThrows — a callback registered on the token throws `InvalidOperationException`; `scope.Cancel()` throws an `AggregateException` holding it
</red>

### Implementation

```csharp
namespace Zerox.Core.Cancellation;

public enum CancellationState { Active, Canceled, TimedOut }

// The root of the cancellation tree, one per application: CancelAll() stops every scope.
public sealed class Terminator(TimeProvider timeProvider) : IDisposable
{
    private readonly CancellationTokenSource _root = new();
    private readonly ScopeChildren _children = new();

    public CancellationToken Token => _root.Token;
    public int Count => _children.Count;

    public CancellationScope CreateScope(TimeSpan? timeout = null)
        => _children.Add(new CancellationScope(_root.Token, timeout, timeProvider, _children));

    public void CancelAll() => _root.Cancel();      // exceptions of callbacks reach the caller (P9)

    public void Dispose()
    {
        _children.DisposeAll();
        _root.Dispose();
    }
}

// A node of the tree: canceled by Cancel(), by its parent, or by its own timeout.
public sealed class CancellationScope : IDisposable
{
    private readonly CancellationToken _parent;
    private readonly CancellationTokenSource? _timeout;
    private readonly CancellationTokenSource _own;
    private readonly TimeProvider _timeProvider;
    private readonly ScopeChildren _siblings;
    private readonly ScopeChildren _children = new();

    internal CancellationScope(CancellationToken parent, TimeSpan? timeout, TimeProvider timeProvider, ScopeChildren siblings)
    {
        _parent = parent;
        _timeProvider = timeProvider;
        _siblings = siblings;
        _timeout = timeout is { } t ? new CancellationTokenSource(t, timeProvider) : null;
        _own = _timeout is null
            ? CancellationTokenSource.CreateLinkedTokenSource(parent)
            : CancellationTokenSource.CreateLinkedTokenSource(parent, _timeout.Token);
    }

    public CancellationToken Token => _own.Token;
    public int Count => _children.Count;

    public CancellationState State
        => _parent.IsCancellationRequested ? CancellationState.Canceled
         : _timeout?.IsCancellationRequested == true ? CancellationState.TimedOut
         : _own.IsCancellationRequested ? CancellationState.Canceled
         : CancellationState.Active;

    public bool IsActive => State == CancellationState.Active;

    public CancellationScope CreateChild(TimeSpan? timeout = null)
        => _children.Add(new CancellationScope(_own.Token, timeout, _timeProvider, _children));

    public void Cancel() => _own.Cancel();

    public void ThrowIfCanceled() => _own.Token.ThrowIfCancellationRequested();

    public Task DelayAsync(TimeSpan delay) => Task.Delay(delay, _timeProvider, _own.Token);

    public void Dispose()
    {
        _children.DisposeAll();
        _siblings.Remove(this);
        _own.Dispose();
        _timeout?.Dispose();
    }
}

// The live children of a node; one lock, no retry loop.
internal sealed class ScopeChildren
{
    private readonly Lock _lock = new();
    private readonly HashSet<CancellationScope> _items = [];
    private bool _disposed;

    public int Count { get { lock (_lock) return _items.Count; } }

    public CancellationScope Add(CancellationScope scope)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _items.Add(scope);
        }
        return scope;
    }

    public void Remove(CancellationScope scope) { lock (_lock) _items.Remove(scope); }

    public void DisposeAll()
    {
        CancellationScope[] items;
        lock (_lock) { _disposed = true; items = [.. _items]; }
        foreach (var item in items)
            item.Dispose();        // each removes itself
    }
}
```

`CreateChild` on a disposed scope throws `ObjectDisposedException` (the child set is disposed).
The state of a child of a canceled parent is `Canceled` even when its own timeout elapsed before:
the parent rule is checked first.

Test shape:

```csharp
namespace Zerox.Core.Tests.Cancellation;

public sealed class CancellationScopeTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Should_BeTimedOut_WhenTheTimeoutElapses()
    {
        using var terminator = new Terminator(_time);
        using var scope = terminator.CreateScope(TimeSpan.FromSeconds(3));
        var delay = scope.DelayAsync(TimeSpan.FromSeconds(10));

        _time.Advance(TimeSpan.FromSeconds(3));

        await FluentActions.Awaiting(() => delay).Should().ThrowAsync<OperationCanceledException>();
        scope.State.Should().Be(CancellationState.TimedOut);
    }
}
```

<success>
- [ ] `CancellationScopeTests` pass (9 tests) without any real wait (`grep -n "Thread.Sleep\|Task.Delay(" tests/Zerox.Core.Tests/Cancellation` is empty)
- [ ] `Microsoft.Extensions.TimeProvider.Testing` is referenced only by test projects
- [ ] No `catch` in `src/Zerox.Core/Cancellation`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="08-owner-queue">
## TDDAB-8: Build the Owner Queue

<intro>
Delivers `OwnerQueue`, the mechanism of assetforge specification §3.7: an object with a state is
touched only through its queue. One consumer runs one job at a time, each to its end with all
its awaits; the caller of `RunAsync` gets a task that completes with the result (or the exception)
of ITS job. The consumer starts when a job arrives and ends when the queue is empty: no thread of
its own, no Start/Stop.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Async/AsyncJobQueue.cs` is the base
(a `Channel`, one reader awaiting each job). Rewritten: no Start/Stop and no counters of starts;
no `ErrorHappen` event — a job's exception goes to the caller of that job (P9: it is not hidden
and not turned into an event); the job receives a `CancellationToken` (the queue's lifetime)
instead of the old `CancellationContext`; no IoC base class, no `INotifyPropertyChanged`.

Two rules of §3.7 are enforced:
- A job never awaits a job of its own queue (it would wait for itself): `RunAsync` called from
  inside a running job of the same queue throws `InvalidOperationException`. The queue knows it
  through an `AsyncLocal` set while a job runs.
- Work a job starts and does not await (a handler) runs in a separate flow: `OwnerQueue.StartDetached(work)`
  starts it with the execution context NOT flowing (so the `AsyncLocal` is not inherited and that
  work may call `RunAsync` later, as an outcome), and an exception escaping it is not hidden: it
  is thrown on the thread pool and stops the process (P9).

`Dispose()`: no new job (`ObjectDisposedException`), the running job's token is canceled, queued
jobs never run and their tasks are canceled.

`PendingCount` = queued + running jobs (for statistics and tests).

The consumer itself never throws (a job's exception is stored in that job's task). If it did, it
would be a bug of the queue: the consumer runs as an `async void` work item on the thread pool so
that such a bug stops the process instead of disappearing in an unobserved task (P9).

Files: `src/Zerox.Core/Concurrency/OwnerQueue.cs`; tests
`tests/Zerox.Core.Tests/Concurrency/OwnerQueueTests.cs`. The old tests target Start/Stop and the
error event and are not ported; the behaviours that remain are covered below.
</intro>

<red>
- test: Should_RunJobsOneAtATimeInOrder_WhenManyAreQueued — 50 jobs queued without awaiting, each awaiting `Task.Yield()` and a short delay, run in queue order and never overlap (maximum concurrency 1)
- test: Should_RunEachJobToItsEnd_WhenItAwaits — job A awaits a `TaskCompletionSource`; job B queued after it has not started until A's source is completed
- test: Should_ReturnTheJobResult_WhenTheCallerAwaits — `await queue.RunAsync(_ => Task.FromResult(42))` is 42
- test: Should_ReachTheCallerWithTheSameException_WhenAJobThrows — the caller of a job that throws `InvalidOperationException("job failed")` gets that exception, same type and message
- test: Should_RunTheNextJob_WhenAPreviousJobThrew — after a failing job, the next queued job runs and returns its result
- test: Should_FailFast_WhenAJobAwaitsAJobOfItsOwnQueue — a job calling `RunAsync` on its own queue gets `InvalidOperationException` (no deadlock: the test completes)
- test: Should_AcceptAJobFromDetachedWork_WhenStartedWithStartDetached — a job starts detached work that calls `RunAsync` on the same queue; that inner job runs after the outer one and completes
- test: Should_CountPendingJobs_WhenQueuedAndRunning — with the first job held, three jobs make `PendingCount` 3; after release it returns to 0
- test: Should_RestartTheConsumer_WhenAJobArrivesAfterTheQueueWentIdle — a job queued after the queue was idle runs
- test: Should_CancelTheRunningJobToken_WhenDisposed — the token received by the running job is canceled by `Dispose()`
- test: Should_CancelQueuedJobs_WhenDisposed — jobs queued behind a held job complete as canceled after `Dispose()` and never run
- test: Should_ThrowObjectDisposed_WhenRunAfterDispose — `RunAsync` after `Dispose()` throws `ObjectDisposedException`
</red>

### Implementation

```csharp
namespace Zerox.Core.Concurrency;

// One job at a time, each to its end with its awaits; the caller gets its own job's result.
// The consumer starts when a job arrives and ends when the queue is empty (no thread of its own).
public sealed class OwnerQueue : IDisposable
{
    private static readonly AsyncLocal<OwnerQueue?> Running = new();

    private readonly Channel<Job> _jobs = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _lifetime = new();
    private int _pending;
    private int _consuming;    // 1 while a consumer runs
    private volatile bool _disposed;

    public int PendingCount => Volatile.Read(ref _pending);

    public Task RunAsync(Func<CancellationToken, Task> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return RunAsync(async token =>
        {
            await job(token).ConfigureAwait(false);
            return true;
        });
    }

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Running.Value == this)
            throw new InvalidOperationException("A job awaited a job of its own queue: it would wait for itself. Start the work with OwnerQueue.StartDetached.");

        var item = new Job<T>(job);
        Interlocked.Increment(ref _pending);
        if (!_jobs.Writer.TryWrite(item))
        {
            Interlocked.Decrement(ref _pending);
            throw new ObjectDisposedException(nameof(OwnerQueue));
        }
        StartConsumerWhenIdle();
        return item.Task;
    }

    // Starts work outside the caller's flow (no AsyncLocal inherited); an exception escaping it stops the process (P9).
    public static void StartDetached(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using (ExecutionContext.SuppressFlow())
            ThreadPool.QueueUserWorkItem(static async w => await w().ConfigureAwait(false), work, preferLocal: false);
    }

    public void Dispose()
    {
        _disposed = true;
        _jobs.Writer.TryComplete();
        _lifetime.Cancel();
        StartConsumerWhenIdle();   // drains: every queued job is canceled
    }

    private void StartConsumerWhenIdle()
    {
        if (Interlocked.CompareExchange(ref _consuming, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(static async queue => await queue.ConsumeAsync().ConfigureAwait(false), this, preferLocal: false);
    }

    private async Task ConsumeAsync()
    {
        while (true)
        {
            while (_jobs.Reader.TryRead(out var job))
            {
                try
                {
                    if (_lifetime.IsCancellationRequested)
                    {
                        job.Cancel();
                        continue;
                    }
                    Running.Value = this;
                    await job.RunAsync(_lifetime.Token).ConfigureAwait(false);
                }
                finally
                {
                    Running.Value = null;
                    Interlocked.Decrement(ref _pending);
                }
            }
            Volatile.Write(ref _consuming, 0);
            // a job written after the last TryRead and before the flag went to 0: take the consumer back
            if (!_jobs.Reader.TryPeek(out _) || Interlocked.CompareExchange(ref _consuming, 1, 0) != 0)
                return;
        }
    }

    private abstract class Job
    {
        public abstract Task RunAsync(CancellationToken token);
        public abstract void Cancel();
    }

    private sealed class Job<T>(Func<CancellationToken, Task<T>> body) : Job
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _completion.Task;

        // the job's outcome, success or exception, goes to its caller
        public override async Task RunAsync(CancellationToken token)
        {
            try
            {
                _completion.SetResult(await body(token).ConfigureAwait(false));
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == token)
            {
                _completion.SetCanceled(token);
            }
            catch (Exception exception)
            {
                _completion.SetException(exception);
            }
        }

        public override void Cancel() => _completion.TrySetCanceled();
    }
}
```

The two `catch` blocks in `Job<T>.RunAsync` do not hide anything: they hand the exception to the
one caller waiting for it (the analyzer CA1031 about catching `Exception` is suppressed there with
a justification comment, nowhere else). `RunContinuationsAsynchronously` keeps the caller's
continuation off the consumer, so a caller never runs inside the next job's slot.

<success>
- [ ] `OwnerQueueTests` pass (12 tests), each in less than 5 s
- [ ] The only `catch` blocks in `src/Zerox.Core/Concurrency/OwnerQueue.cs` are the two in `Job<T>.RunAsync`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
