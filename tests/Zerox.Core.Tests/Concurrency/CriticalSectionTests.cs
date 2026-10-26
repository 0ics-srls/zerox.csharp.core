using Zerox.Core.Concurrency;

namespace Zerox.Core.Tests.Concurrency;

public sealed class CriticalSectionTests
{
    // Counts an optimistic check around a pause: without mutual exclusion another worker gets in between.
    private sealed class Counters
    {
        private readonly Random _random = new(7);
        public int OptimisticLock;
        public int Good;
        public int Wrong;

        public void Work()
        {
            var seen = OptimisticLock;
            Thread.SpinWait(_random.Next(1_000, 5_000));   // a chance for the workers to mix if the lock does not work
            ++OptimisticLock;
            if (OptimisticLock == seen + 1)
                ++Good;
            else
                ++Wrong;
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Test_CriticalSection()
    {
        using var cs = new CriticalSection();
        var counters = new Counters();
        using var start = new ManualResetEventSlim(false);

        var workers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 100; ++i)
            {
                using (cs.Lock(TimeSpan.FromSeconds(15)))
                    counters.Work();
                Thread.Yield();
            }
        })).ToArray();
        start.Set();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(0, counters.Wrong);
        Assert.Equal(200, counters.Good);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Test_No_CriticalSection()
    {
        var counters = new Counters();
        using var start = new ManualResetEventSlim(false);

        var workers = Enumerable.Range(0, 10).Select(_ => Task.Factory.StartNew(() =>
        {
            start.Wait();
            for (var i = 0; i < 100; ++i)
                counters.Work();
        }, TaskCreationOptions.LongRunning)).ToArray();
        start.Set();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

        Assert.True(counters.Wrong > 0);
        Assert.NotEqual(1000, counters.Good);
    }

    [Fact]
    public async Task Test_CriticalSectionAsyncCompatibilityAsync()
    {
        using var cs = new CriticalSection();

        var exception = await Record.ExceptionAsync(async () =>
        {
#pragma warning disable CA1849 // the test proves that the synchronous Lock holds across an await
            using (cs.Lock(TimeSpan.FromSeconds(100)))
                await Task.Delay(100, TestContext.Current.CancellationToken);
#pragma warning restore CA1849
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task Should_RunOneBodyAtATime_WhenFiftyTasksLockAsync()
    {
        using var cs = new CriticalSection();
        var inside = 0;
        var maximum = 0;
        var completed = 0;

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            using (await cs.LockAsync(TestContext.Current.CancellationToken))
            {
                var now = Interlocked.Increment(ref inside);
                InterlockedMax(ref maximum, now);
                await Task.Yield();
                await Task.Delay(1, TestContext.Current.CancellationToken);
                Interlocked.Decrement(ref inside);
            }
            Interlocked.Increment(ref completed);
        })));

        maximum.Should().Be(1);
        completed.Should().Be(50);
    }

    [Fact]
    public async Task Should_ReturnAnUnlockedHandle_WhenTryLockFindsItHeld()
    {
        using var cs = new CriticalSection();
        using var held = await cs.LockAsync(TestContext.Current.CancellationToken);

        TryNow(cs).Should().BeFalse();
        (await cs.TryLockAsync(TimeSpan.Zero, TestContext.Current.CancellationToken)).IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ThrowTimeout_WhenLockWithTimeoutFindsItHeld()
    {
        using var cs = new CriticalSection();
        using var held = await cs.LockAsync(TestContext.Current.CancellationToken);

        FluentActions.Invoking(() => cs.Lock(TimeSpan.FromMilliseconds(10))).Should().Throw<TimeoutException>();
        await FluentActions.Awaiting(async () => await cs.LockAsync(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_WhenLockAsyncIsCanceled()
    {
        using var cs = new CriticalSection();
        using var held = await cs.LockAsync(TestContext.Current.CancellationToken);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await FluentActions.Awaiting(async () => await cs.LockAsync(canceled.Token)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Should_ReleaseOnce_WhenTheHandleIsDisposed()
    {
        using var cs = new CriticalSection();
        var held = cs.Lock();
        var notAcquired = cs.TryLock(TimeSpan.Zero);
        notAcquired.IsLocked.Should().BeFalse();
        notAcquired.Dispose();   // releases nothing: the section stays held

        cs.TryLock(TimeSpan.Zero).IsLocked.Should().BeFalse();
        held.Dispose();

        using var again = cs.TryLock(TimeSpan.Zero);
        again.IsLocked.Should().BeTrue();
    }

    // synchronous on purpose: the synchronous TryLock is the one under test
    private static bool TryNow(CriticalSection cs) => cs.TryLock(TimeSpan.Zero).IsLocked;

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
