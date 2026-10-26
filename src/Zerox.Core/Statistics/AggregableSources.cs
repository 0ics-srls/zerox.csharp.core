using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Zerox.Core.Identity;
using Zerox.Core.Reflection;

namespace Zerox.Core.Statistics;

// Discovers the [Aggregable] properties of services, creates their series in the aggregator and samples them.
public sealed class AggregableSources(SeriesAggregator aggregator, TimeProvider timeProvider, int capacity = 1000)
{
    private static readonly HashSet<Type> Numeric =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    ];

    private readonly SeriesAggregator _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
    private readonly TimeProvider _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ConcurrentDictionary<IAggregableSource, Source> _sources = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<DataSeries<DateTime, double>> Register(IAggregableSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var probes = source.GetType().GetPropertiesWith<AggregableAttribute>()
            .Select(marked => CreateProbe(source, marked.Property, marked.Attribute))
            .ToArray();
        var entry = new Source(source, probes);
        if (!_sources.TryAdd(source, entry))
            throw new ArgumentException($"The source {source.Name} is already registered", nameof(source));
        for (var i = 0; i < probes.Length; i++)
        {
            if (!_aggregator.TryRegister(probes[i].Series))
            {
                foreach (var registered in probes.Take(i))
                    _aggregator.Remove(registered.Series.Name);
                _sources.TryRemove(source, out _);
                throw new ArgumentException($"A series named {probes[i].Series.Name} is already registered", nameof(source));
            }
            if (probes[i].Group is { } group)
                _aggregator.TryRegister(SeriesGroup.WithPrefix(group, HierId.From(group)));
        }
        source.PropertyChanged += entry.OnPropertyChanged;
        return [.. probes.Select(probe => probe.Series)];
    }

    public bool Unregister(IAggregableSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!_sources.TryRemove(source, out var entry))
            return false;
        source.PropertyChanged -= entry.OnPropertyChanged;
        foreach (var probe in entry.Probes)
            _aggregator.Remove(probe.Series.Name);
        return true;
    }

    // Samples every marked property of every registered source.
    public void Sample()
    {
        foreach (var entry in _sources.Values)
            entry.SampleAll();
    }

    private Probe CreateProbe(IAggregableSource source, PropertyInfo property, AggregableAttribute attribute)
    {
        if (!Numeric.Contains(property.PropertyType))
            throw new ArgumentException($"{source.Name}.{property.Name} is [Aggregable] but not numeric", nameof(source));
        var getter = FastProperty.For<object, double>(property);
        if (!getter.CanRead)
            throw new ArgumentException($"{source.Name}.{property.Name} is [Aggregable] but has no public getter", nameof(source));
        var name = attribute.Name ?? property.Name;
        var id = attribute.Group is { } group ? HierId.From(group, source.Name, name) : HierId.From(source.Name, name);
        var series = new DataSeries<DateTime, double>($"{source.Name}.{name}", new TimeKeys(_time), capacity, id);
        return new(property.Name, attribute.Group, getter, series);
    }

    private sealed record Probe(string Property, string? Group, FastProperty<object, double> Getter, DataSeries<DateTime, double> Series)
    {
        public void Sample(object owner) => Series.Add(Getter.Get(owner));
    }

    private sealed class Source(IAggregableSource owner, Probe[] probes)
    {
        // The source being sampled on this thread: its own PropertyChanged raised by a getter is not sampled again.
        [ThreadStatic]
        private static Source? _sampling;

        public Probe[] Probes => probes;

        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_sampling == this)
                return;
            foreach (var probe in probes)
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == probe.Property)
                    probe.Sample(owner);
            }
        }

        public void SampleAll()
        {
            var outer = _sampling;
            _sampling = this;
            try
            {
                foreach (var probe in probes)
                    probe.Sample(owner);
            }
            finally
            {
                _sampling = outer;
            }
        }
    }
}
