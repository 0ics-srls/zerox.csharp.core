using System.Numerics;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A numeric series: the last `capacity` points, sorted by key, and running statistics over every
// sample since the last Clear. One lock: an add is atomic, a reader never sees half of it.
public sealed class DataSeries<TKey, TValue> : IValueSeries<TValue>
    where TKey : struct, IComparable<TKey>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly ISeriesKeys<TKey> _keys;
    private readonly CircularBuffer<KeyValuePair<TKey, TValue>> _points;
    private readonly IComparer<KeyValuePair<TKey, TValue>> _byKey = KeyComparer.For<TKey, TValue>();
    private long _count;
    private TValue _min;
    private TValue _max;
    private double _sum;
    private double _rate;
    private KeyValuePair<TKey, TValue> _last;

    public DataSeries(string name, ISeriesKeys<TKey> keys, int capacity = 1000, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(keys);
        Name = name;
        Id = id ?? HierId.From(name);
        _keys = keys;
        _points = new(capacity);
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Capacity => _points.Capacity;

    public int StoredCount => _points.Count;

    public long Count { get { lock (_lock) return _count; } }

    public TValue Min { get { lock (_lock) return _min; } }

    public TValue Max { get { lock (_lock) return _max; } }

    public TValue Last { get { lock (_lock) return _last.Value; } }

    public KeyValuePair<TKey, TValue> LastPair { get { lock (_lock) return _last; } }

    public double Sum { get { lock (_lock) return _sum; } }

    public double Mean { get { lock (_lock) return _count == 0 ? 0 : _sum / _count; } }

    // Value change per key distance unit between the last two samples (per second with TimeKeys).
    public double Rate { get { lock (_lock) return _rate; } }

    public void Add(TValue value)
    {
        lock (_lock)
            Append(value, _keys.NextKey());
    }

    public void Add(TValue value, TKey key)
    {
        lock (_lock)
            Append(value, key);
    }

    public KeyValuePair<TKey, TValue>[] View(Interval<TKey> interval)
        => _points.View(ToPoints(interval), _byKey);

    // Sum of the values with from <= key < to.
    public double ViewSum(TKey from, TKey to)
    {
        var sum = 0.0;
        foreach (var point in View(Interval.Range(from, to)))
            sum += double.CreateTruncating(point.Value);
        return sum;
    }

    // The stored point at index (0 = the oldest stored).
    public KeyValuePair<TKey, TValue> Get(int index) => _points[index];

    public KeyValuePair<TKey, TValue>[] Snapshot() => _points.ToArray();

    // Replaces everything with the points (ascending keys); the statistics are recomputed from them.
    public void Load(ReadOnlySpan<KeyValuePair<TKey, TValue>> points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            if (points[i].Key.CompareTo(points[i - 1].Key) < 0)
                throw new ArgumentException($"The keys of the points must be ascending (index {i})", nameof(points));
        }
        lock (_lock)
        {
            ClearUnlocked();
            foreach (var point in points)
                Append(point.Value, point.Key);
        }
    }

    public void Clear()
    {
        lock (_lock)
            ClearUnlocked();
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var points = _points.View(Interval.LastN<KeyValuePair<TKey, TValue>>(SeriesWindow.NewPoints(count, _count, _points.Count)));
            return new(Name, _count, points);
        }
    }

    private void Append(TValue value, TKey key)
    {
        if (_count > 0 && key.CompareTo(_last.Key) < 0)
            throw new ArgumentOutOfRangeException(nameof(key), key, $"The keys of {Name} must not go back (last {_last.Key})");
        var sample = double.CreateTruncating(value);
        if (_count == 0)
        {
            _min = _max = value;
            _rate = 0;
        }
        else
        {
            _min = TValue.Min(_min, value);
            _max = TValue.Max(_max, value);
            var distance = _keys.Distance(_last.Key, key);
            if (distance > 0)
                _rate = (sample - double.CreateTruncating(_last.Value)) / distance;
        }
        _sum += sample;
        _count++;
        _last = new(key, value);
        _points.PushBack(_last);
    }

    private void ClearUnlocked()
    {
        _points.Clear();
        _count = 0;
        _min = _max = TValue.Zero;
        _sum = _rate = 0;
        _last = default;
    }

    private static Interval<KeyValuePair<TKey, TValue>> ToPoints(Interval<TKey> interval)
        => new(interval.From is TKey from ? new(from, default) : null,
               interval.To is TKey to ? new(to, default) : null,
               interval.MaxLastCount);
}
