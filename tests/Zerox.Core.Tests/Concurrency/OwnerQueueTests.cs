using System.Collections.Concurrent;
using Zerox.Core.Concurrency;

namespace Zerox.Core.Tests.Concurrency;

public sealed class OwnerQueueTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_RunJobsOneAtATimeInOrder_WhenManyAreQueued()
    {
        using var queue = new OwnerQueue();
        var order = new ConcurrentQueue<int>();
        var inside = 0;
        var maximum = 0;

        var jobs = Enumerable.Range(0, 50).Select(i => queue.RunAsync(async _ =>
        {
            var now = Interlocked.Increment(ref inside);
            maximum = Math.Max(maximum, now);
            await Task.Yield();
            await Task.Delay(1, Ct);
            order.Enqueue(i);
            Interlocked.Decrement(ref inside);
        })).ToArray();
        await Task.WhenAll(jobs).WaitAsync(Limit, Ct);

        order.Should().Equal(Enumerable.Range(0, 50));
        maximum.Should().Be(1);
    }

    [Fact]
    public async Task Should_RunEachJobToItsEnd_WhenItAwaits()
    {
        using var queue = new OwnerQueue();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bStarted = false;

        var a = queue.RunAsync(async _ =>
        {
            aStarted.SetResult();
            await release.Task;
        });
        var b = queue.RunAsync(_ =>
        {
            bStarted = true;
            return Task.CompletedTask;
        });
        await aStarted.Task.WaitAsync(Limit, Ct);
        await Task.Delay(50, Ct);

        bStarted.Should().BeFalse();
        release.SetResult();
        await Task.WhenAll(a, b).WaitAsync(Limit, Ct);
        bStarted.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnTheJobResult_WhenTheCallerAwaits()
    {
        using var queue = new OwnerQueue();

        (await queue.RunAsync(_ => Task.FromResult(42)).WaitAsync(Limit, Ct)).Should().Be(42);
    }

    [Fact]
    public async Task Should_ReachTheCallerWithTheSameException_WhenAJobThrows()
    {
        using var queue = new OwnerQueue();

        var run = () => queue.RunAsync(_ => Task.FromException(new InvalidOperationException("job failed")));

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("job failed");
    }

    [Fact]
    public async Task Should_RunTheNextJob_WhenAPreviousJobThrew()
    {
        using var queue = new OwnerQueue();
        var failing = queue.RunAsync<int>(_ => throw new InvalidOperationException("job failed"));
        var next = queue.RunAsync(_ => Task.FromResult(7));

        await FluentActions.Awaiting(() => failing).Should().ThrowAsync<InvalidOperationException>();
        (await next.WaitAsync(Limit, Ct)).Should().Be(7);
    }

    [Fact]
    public async Task Should_FailFast_WhenAJobAwaitsAJobOfItsOwnQueue()
    {
        using var queue = new OwnerQueue();

        var outer = queue.RunAsync(async _ => await queue.RunAsync(_ => Task.FromResult(1)));

        await FluentActions.Awaiting(() => outer.WaitAsync(Limit, Ct)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_AcceptAJobFromDetachedWork_WhenStartedWithStartDetached()
    {
        using var queue = new OwnerQueue();
        var steps = new ConcurrentQueue<string>();
        var inner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.RunAsync(_ =>
        {
            OwnerQueue.StartDetached(async () =>
            {
                await queue.RunAsync(_ =>
                {
                    steps.Enqueue("inner");
                    return Task.CompletedTask;
                });
                inner.SetResult();
            });
            steps.Enqueue("outer");
            return Task.CompletedTask;
        }).WaitAsync(Limit, Ct);
        await inner.Task.WaitAsync(Limit, Ct);

        steps.Should().Equal("outer", "inner");
    }

    [Fact]
    public async Task Should_CountPendingJobs_WhenQueuedAndRunning()
    {
        using var queue = new OwnerQueue();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = queue.RunAsync(async _ => await release.Task);
        var second = queue.RunAsync(_ => Task.CompletedTask);
        var third = queue.RunAsync(_ => Task.CompletedTask);

        queue.PendingCount.Should().Be(3);
        release.SetResult();
        await Task.WhenAll(held, second, third).WaitAsync(Limit, Ct);

        queue.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RestartTheConsumer_WhenAJobArrivesAfterTheQueueWentIdle()
    {
        using var queue = new OwnerQueue();
        await queue.RunAsync(_ => Task.CompletedTask).WaitAsync(Limit, Ct);
        await Task.Delay(50, Ct);

        (await queue.RunAsync(_ => Task.FromResult("again")).WaitAsync(Limit, Ct)).Should().Be("again");
    }

    [Fact]
    public async Task Should_CancelTheRunningJobToken_WhenDisposed()
    {
        var queue = new OwnerQueue();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = queue.RunAsync(async token =>
        {
            started.SetResult(token);
            await Task.Delay(Timeout.Infinite, token);
        });
        var token = await started.Task.WaitAsync(Limit, Ct);

        queue.Dispose();

        token.IsCancellationRequested.Should().BeTrue();
        await FluentActions.Awaiting(() => running.WaitAsync(Limit, Ct)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_CancelQueuedJobs_WhenDisposed()
    {
        var queue = new OwnerQueue();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedRan = false;
        var held = queue.RunAsync(async _ =>
        {
            started.SetResult();
            await release.Task;
        });
        var queued = queue.RunAsync(_ =>
        {
            queuedRan = true;
            return Task.CompletedTask;
        });
        await started.Task.WaitAsync(Limit, Ct);

        queue.Dispose();
        release.SetResult();
        await held.WaitAsync(Limit, Ct);

        await FluentActions.Awaiting(() => queued.WaitAsync(Limit, Ct)).Should().ThrowAsync<OperationCanceledException>();
        queuedRan.Should().BeFalse();
    }

    [Fact]
    public void Should_ThrowObjectDisposed_WhenRunAfterDispose()
    {
        var queue = new OwnerQueue();
        queue.Dispose();

        // RunAsync refuses synchronously: no task is returned
        Action run = () => _ = queue.RunAsync(_ => Task.CompletedTask);

        run.Should().Throw<ObjectDisposedException>();
    }
}
