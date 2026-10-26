using System.Threading.Channels;
using Zerox.Core.Concurrency;

namespace Zerox.Core.Statistics;

// Periodically sends the new points of chosen series (or groups) through the Updates channel.
public sealed class RemoteDataProvider : IAsyncDisposable
{
    private readonly SeriesAggregator _aggregator;
    private readonly TimeProvider _time;
    private readonly TimeSpan _period;
    private readonly Channel<SeriesUpdate> _updates = Channel.CreateUnbounded<SeriesUpdate>(new() { SingleWriter = true });
    private readonly Lock _lock = new();
    private PeriodicTimer? _timer;
    private Task _stopped = Task.CompletedTask;

    public RemoteDataProvider(SeriesAggregator aggregator, TimeProvider timeProvider, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _aggregator = aggregator;
        _time = timeProvider;
        _period = period;
    }

    public static TimeSpan DefaultPeriod { get; } = TimeSpan.FromMilliseconds(250);

    public ChannelReader<SeriesUpdate> Updates => _updates.Reader;

    public IReadOnlyList<string> AvailableSeries() => _aggregator.AvailableNames<ISeries>();

    public void Start(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        string[] crawled = [.. names];
        foreach (var name in crawled)
        {
            if (_aggregator.GetSeriesOrGroup<ISeries>(name).Count == 0)
                throw new ArgumentException($"No series or group named {name}", nameof(names));
        }
        lock (_lock)
        {
            if (_timer is not null)
                throw new InvalidOperationException("The provider is already crawling: stop it first");
            var timer = new PeriodicTimer(_period, _time);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _timer = timer;
            _stopped = stopped.Task;
            OwnerQueue.StartDetached(() => CrawlAsync(timer, crawled, stopped));
        }
    }

    public async Task StopAsync()
    {
        PeriodicTimer? timer;
        Task stopped;
        lock (_lock)
        {
            timer = _timer;
            stopped = _stopped;
            _timer = null;
        }
        if (timer is null)
            return;
        timer.Dispose();   // the pending wait returns false: the loop ends
        await stopped.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _updates.Writer.TryComplete();
    }

    private async Task CrawlAsync(PeriodicTimer timer, string[] names, TaskCompletionSource stopped)
    {
        try
        {
            var sent = new Dictionary<string, long>(StringComparer.Ordinal);
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
                Collect(names, sent);
        }
        finally
        {
            stopped.SetResult();
        }
    }

    private void Collect(string[] names, Dictionary<string, long> sent)
    {
        var tick = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            foreach (var series in _aggregator.GetSeriesOrGroup<ISeries>(name))
            {
                if (!tick.Add(series.Name))
                    continue;
                var update = series.UpdateSince(sent.GetValueOrDefault(series.Name));
                sent[series.Name] = update.Count;
                if (update.Points.Length > 0)
                    _updates.Writer.TryWrite(update);
            }
        }
    }
}
