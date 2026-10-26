using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A series of rows (one value per channel): the last `capacity` rows and per-channel statistics.
public sealed class ArraySeries<TKey, TValue> : ISeries
    where TKey : struct, IComparable<TKey>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly ISeriesKeys<TKey> _keys;
    private readonly CircularBuffer<KeyValuePair<TKey, ImmutableArray<TValue>>> _rows;
    private readonly IComparer<KeyValuePair<TKey, ImmutableArray<TValue>>> _byKey = KeyComparer.For<TKey, ImmutableArray<TValue>>();
    private readonly TValue[] _min;
    private readonly TValue[] _max;
    private readonly TValue[] _last;
    private readonly double[] _sum;
    private readonly double[] _rate;
    private TKey _lastKey;
    private long _count;

    public ArraySeries(string name, int width, ISeriesKeys<TKey> keys, int capacity = 1000, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentNullException.ThrowIfNull(keys);
        Name = name;
        Id = id ?? HierId.From(name);
        _keys = keys;
        _rows = new(capacity);
        _min = new TValue[width];
        _max = new TValue[width];
        _last = new TValue[width];
        _sum = new double[width];
        _rate = new double[width];
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Width => _last.Length;

    public int Capacity => _rows.Capacity;

    public int StoredCount => _rows.Count;

    public long Count { get { lock (_lock) return _count; } }

    public ImmutableArray<TValue> Min { get { lock (_lock) return [.. _min]; } }

    public ImmutableArray<TValue> Max { get { lock (_lock) return [.. _max]; } }

    public ImmutableArray<TValue> Last { get { lock (_lock) return [.. _last]; } }

    public ImmutableArray<double> Sum { get { lock (_lock) return [.. _sum]; } }

    public ImmutableArray<double> Mean { get { lock (_lock) return [.. _sum.Select(sum => _count == 0 ? 0 : sum / _count)]; } }

    public ImmutableArray<double> Rate { get { lock (_lock) return [.. _rate]; } }

    public KeyValuePair<TKey, ImmutableArray<TValue>> LastRow
    {
        get
        {
            lock (_lock)
                return _count == 0 ? default : new(_lastKey, [.. _last]);
        }
    }

    public void Add(ReadOnlySpan<TValue> row)
    {
        lock (_lock)
            Append(row, _keys.NextKey());
    }

    public void Add(ReadOnlySpan<TValue> row, TKey key)
    {
        lock (_lock)
            Append(row, key);
    }

    // The parts joined in order form the row (several boards read into one row).
    public void AddParts(params TValue[][] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Add(parts.SelectMany(part => part).ToArray());
    }

    public KeyValuePair<TKey, ImmutableArray<TValue>>[] View(Interval<TKey> interval)
        => _rows.View(new(interval.From is TKey from ? new(from, default) : null,
                          interval.To is TKey to ? new(to, default) : null,
                          interval.MaxLastCount), _byKey);

    public KeyValuePair<TKey, TValue>[] ViewChannel(Interval<TKey> interval, int channel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, Width);
        return [.. View(interval).Select(row => new KeyValuePair<TKey, TValue>(row.Key, row.Value[channel]))];
    }

    // Per channel, the sum of the values with from <= key < to.
    public double[] ViewSum(TKey from, TKey to)
    {
        var sums = new double[Width];
        foreach (var row in View(Interval.Range(from, to)))
        {
            for (var i = 0; i < sums.Length; i++)
                sums[i] += double.CreateTruncating(row.Value[i]);
        }
        return sums;
    }

    public KeyValuePair<TKey, ImmutableArray<TValue>> Get(int index) => _rows[index];

    public void Clear()
    {
        lock (_lock)
        {
            _rows.Clear();
            Array.Clear(_min);
            Array.Clear(_max);
            Array.Clear(_last);
            Array.Clear(_sum);
            Array.Clear(_rate);
            _lastKey = default;
            _count = 0;
        }
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var rows = _rows.View(Interval.LastN<KeyValuePair<TKey, ImmutableArray<TValue>>>(SeriesWindow.NewPoints(count, _count, _rows.Count)));
            return new(Name, _count, rows);
        }
    }

    private void Append(ReadOnlySpan<TValue> row, TKey key)
    {
        if (row.Length != Width)
            throw new ArgumentException($"A row of {Name} has {Width} values, not {row.Length}", nameof(row));
        if (_count > 0 && key.CompareTo(_lastKey) < 0)
            throw new ArgumentOutOfRangeException(nameof(key), key, $"The keys of {Name} must not go back (last {_lastKey})");
        var distance = _count == 0 ? 0 : _keys.Distance(_lastKey, key);
        for (var i = 0; i < row.Length; i++)
        {
            var value = row[i];
            var sample = double.CreateTruncating(value);
            if (_count == 0)
            {
                _min[i] = _max[i] = value;
                _rate[i] = 0;
            }
            else
            {
                _min[i] = TValue.Min(_min[i], value);
                _max[i] = TValue.Max(_max[i], value);
                if (distance > 0)
                    _rate[i] = (sample - double.CreateTruncating(_last[i])) / distance;
            }
            _sum[i] += sample;
            _last[i] = value;
        }
        _count++;
        _lastKey = key;
        _rows.PushBack(new(key, ImmutableCollectionsMarshal.AsImmutableArray(row.ToArray())));
    }
}
