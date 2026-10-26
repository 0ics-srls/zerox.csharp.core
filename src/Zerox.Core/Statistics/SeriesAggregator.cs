using System.Collections.Concurrent;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// The registry of series by name, by id, by group.
public sealed class SeriesAggregator
{
    private readonly ConcurrentDictionary<string, ISeries> _series = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SeriesGroup> _groups = new(StringComparer.Ordinal);

    public IReadOnlyList<ISeries> Series => [.. _series.Values.OrderBy(series => series.Name, StringComparer.Ordinal)];

    public bool TryRegister(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return _series.TryAdd(series.Name, series);
    }

    public void Register(ISeries series)
    {
        if (!TryRegister(series))
            throw new ArgumentException($"A series named {series.Name} is already registered", nameof(series));
    }

    public bool TryRegister(SeriesGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return _groups.TryAdd(group.Name, group);
    }

    public void Register(SeriesGroup group)
    {
        if (!TryRegister(group))
            throw new ArgumentException($"A group named {group.Name} is already registered", nameof(group));
    }

    public bool Remove(string name) => _series.TryRemove(name, out _);

    public void Add<TValue>(string name, TValue value)
    {
        var series = Find(name) as IValueSeries<TValue>
            ?? throw new ArgumentException($"The series {name} does not take {typeof(TValue).Name} values", nameof(value));
        series.Add(value);
    }

    public TSeries Get<TSeries>(string name)
        where TSeries : class, ISeries
        => Find(name) as TSeries
            ?? throw new ArgumentException($"The series {name} is not a {typeof(TSeries).Name}", nameof(name));

    public IReadOnlyList<TSeries> GetByHierId<TSeries>(Predicate<HierId> predicate)
        where TSeries : class, ISeries
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return [.. _series.Values.OfType<TSeries>().Where(series => predicate(series.Id)).OrderBy(series => series.Name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<TSeries> GetSeriesOrGroup<TSeries>(string name)
        where TSeries : class, ISeries
    {
        if (_series.TryGetValue(name, out var series))
            return series is TSeries typed ? [typed] : [];
        return _groups.TryGetValue(name, out var group) ? GetByHierId<TSeries>(group.Contains) : [];
    }

    public IReadOnlyList<string> AvailableNames<TSeries>()
        where TSeries : class, ISeries
        => [.. _series.Values.OfType<TSeries>().Select(series => series.Name)
            .Concat(_groups.Keys.Where(group => GetSeriesOrGroup<TSeries>(group).Count > 0))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private ISeries Find(string name)
        => _series.TryGetValue(name, out var series) ? series : throw new ArgumentException($"No series named {name}", nameof(name));
}
