using Zerox.Core.Concurrency;

namespace Zerox.Core.Scheduling;

// Runs an async cycle at once and then once per period (a cycle is awaited to its end before the next
// wait); an exception escaping the cycle stops the process — except the cancellation the stop itself
// asked for, which ends the loop.
public sealed class WorkingProcess : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _cycle;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _stop;
    private Task _stopped = Task.CompletedTask;
    private long _cycles;
    private long _lastTicks;
    private long _totalTicks;

    public WorkingProcess(string name, Func<CancellationToken, Task> cycle, TimeSpan period, TimeProvider timeProvider)
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
                try
                {
                    await _cycle(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    break;   // the stop cancelled the cycle in flight: the end it asked for, not a failure
                }
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
