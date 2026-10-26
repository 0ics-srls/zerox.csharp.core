using System.Numerics;
using Zerox.Core.Collections;
using Zerox.Core.Identity;

namespace Zerox.Core.Statistics;

// A moving window of the last `size` values: sum and mean of the window, the extremes the mean
// has reached (MinMean, MaxMean), the extremes of the values now in the window (Min, Max).
public sealed class CyclicSeries<TValue> : IValueSeries<TValue>
    where TValue : struct, INumber<TValue>
{
    private readonly Lock _lock = new();
    private readonly CircularBuffer<TValue> _window;
    private double _sum;
    private double _minMean;
    private double _maxMean;
    private TValue _last;
    private long _count;

    public CyclicSeries(string name, int size, HierId? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Id = id ?? HierId.From(name);
        _window = new(size);
    }

    public string Name { get; }

    public HierId Id { get; }

    public int Size => _window.Capacity;

    public int WindowCount => _window.Count;

    public long Count { get { lock (_lock) return _count; } }

    public TValue[] Values => _window.ToArray();

    public TValue Last { get { lock (_lock) return _last; } }

    public double Sum { get { lock (_lock) return _sum; } }

    public double Mean { get { lock (_lock) return _window.Count == 0 ? 0 : _sum / _window.Count; } }

    public double MinMean { get { lock (_lock) return _minMean; } }

    public double MaxMean { get { lock (_lock) return _maxMean; } }

    public TValue Min
    {
        get
        {
            var values = Values;
            return values.Length == 0 ? TValue.Zero : values.Min();
        }
    }

    public TValue Max
    {
        get
        {
            var values = Values;
            return values.Length == 0 ? TValue.Zero : values.Max();
        }
    }

    public void Add(TValue value)
    {
        lock (_lock)
        {
            if (_window.PushBack(value, out var left))
                _sum -= double.CreateTruncating(left);
            _sum += double.CreateTruncating(value);
            _last = value;
            _count++;
            var mean = _sum / _window.Count;
            _minMean = _count == 1 ? mean : Math.Min(_minMean, mean);
            _maxMean = _count == 1 ? mean : Math.Max(_maxMean, mean);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _window.Clear();
            _sum = _minMean = _maxMean = 0;
            _last = TValue.Zero;
            _count = 0;
        }
    }

    public SeriesUpdate UpdateSince(long count)
    {
        lock (_lock)
        {
            var values = _window.View(Interval.LastN<TValue>(SeriesWindow.NewPoints(count, _count, _window.Count)));
            return new(Name, _count, values);
        }
    }
}
