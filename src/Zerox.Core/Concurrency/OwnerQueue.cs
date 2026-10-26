using System.Threading.Channels;

namespace Zerox.Core.Concurrency;

// One job at a time, each to its end with its awaits; the caller gets its own job's result.
// The consumer starts when a job arrives and ends when the queue is empty (no thread of its own).
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The name of the mechanism in assetforge specification §3.7")]
public sealed class OwnerQueue : IDisposable
{
    private static readonly AsyncLocal<OwnerQueue?> Running = new();

    private readonly Channel<Job> _jobs = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _lifetime = new();
    private int _pending;
    private int _consuming;    // 1 while a consumer runs
    private volatile bool _disposed;

    // Queued plus running jobs.
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

    // Starts work outside the caller's flow (nothing of the caller's AsyncLocal state is inherited, so the work
    // may queue jobs later); an exception escaping the work stops the process instead of disappearing.
    public static void StartDetached(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using (ExecutionContext.SuppressFlow())
            ThreadPool.QueueUserWorkItem(static async w => await w().ConfigureAwait(false), work, preferLocal: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _jobs.Writer.TryComplete();
        _lifetime.Cancel();
        StartConsumerWhenIdle();   // drains: every queued job is canceled
    }

    // The consumer is an async void work item: it never throws, and if a bug made it throw, the process would stop.
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

        // The job's outcome, success or exception, goes to its caller: nothing is hidden, it changes hands.
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
#pragma warning disable CA1031 // the exception is handed to the one caller waiting for it
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _completion.SetException(exception);
            }
        }

        public override void Cancel() => _completion.TrySetCanceled();
    }
}
