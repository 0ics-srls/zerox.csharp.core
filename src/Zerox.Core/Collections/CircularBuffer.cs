using System.Collections;

namespace Zerox.Core.Collections;

// A ring of fixed capacity. Full: a push overwrites the item at the other end and returns it.
// Thread safe (one lock); a view or ToArray is a copy.
public sealed class CircularBuffer<T> : IEnumerable<T>
    where T : struct
{
    private readonly Lock _lock = new();
    private readonly T[] _items;
    private int _start;   // first item
    private int _end;     // one after the last item
    private int _count;

    public CircularBuffer(int capacity)
        : this(capacity, [])
    {
    }

    public CircularBuffer(int capacity, ReadOnlySpan<T> items)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        if (items.Length > capacity)
            throw new ArgumentException($"{items.Length} items do not fit a capacity of {capacity}", nameof(items));
        _items = new T[capacity];
        items.CopyTo(_items);
        _count = items.Length;
        _end = _count == capacity ? 0 : _count;
    }

    public int Capacity => _items.Length;

    public int Count
    {
        get
        {
            lock (_lock)
                return _count;
        }
    }

    public bool IsEmpty => Count == 0;

    public bool IsFull => Count == Capacity;

    public T this[int index]
    {
        get
        {
            lock (_lock)
                return _items[Physical(index)];
        }
        set
        {
            lock (_lock)
                _items[Physical(index)] = value;
        }
    }

    public T Front()
    {
        lock (_lock)
        {
            ThrowIfEmpty();
            return _items[_start];
        }
    }

    public T Back()
    {
        lock (_lock)
        {
            ThrowIfEmpty();
            return _items[Previous(_end)];
        }
    }

    // Appends at the back; when full, the front item is overwritten, returned in removed, and the result is true.
    public bool PushBack(T item, out T removed)
    {
        lock (_lock)
        {
            removed = default;
            var full = _count == _items.Length;
            if (full)
                removed = _items[_end];
            _items[_end] = item;
            _end = Next(_end);
            if (full)
                _start = _end;
            else
                _count++;
            return full;
        }
    }

    public void PushBack(T item) => PushBack(item, out _);

    // Prepends at the front; when full, the back item is overwritten, returned in removed, and the result is true.
    public bool PushFront(T item, out T removed)
    {
        lock (_lock)
        {
            removed = default;
            var full = _count == _items.Length;
            _start = Previous(_start);
            if (full)
            {
                removed = _items[_start];
                _end = _start;
            }
            else
            {
                _count++;
            }
            _items[_start] = item;
            return full;
        }
    }

    public void PushFront(T item) => PushFront(item, out _);

    public T PopBack()
    {
        lock (_lock)
        {
            ThrowIfEmpty();
            _end = Previous(_end);
            var item = _items[_end];
            _items[_end] = default;
            _count--;
            return item;
        }
    }

    public T PopFront()
    {
        lock (_lock)
        {
            ThrowIfEmpty();
            var item = _items[_start];
            _items[_start] = default;
            _start = Next(_start);
            _count--;
            return item;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_items);
            _start = _end = _count = 0;
        }
    }

    public T[] ToArray()
    {
        lock (_lock)
        {
            var first = FirstSegment();
            var result = new T[_count];
            first.CopyTo(result);
            SecondSegment().CopyTo(result.AsSpan(first.Length));
            return result;
        }
    }

    // A copy of the items inside the interval; the buffer is sorted ascending for the comparer.
    public T[] View(Interval<T> interval, IComparer<T>? comparer = null)
    {
        if (interval.MaxLastCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(interval), interval.MaxLastCount, "MaxLastCount must not be negative");
        lock (_lock)
        {
            var first = Slice(FirstSegment(), interval, comparer);
            var second = Slice(SecondSegment(), interval, comparer);
            var total = first.Length + second.Length;
            var skip = interval.MaxLastCount is int max && max < total ? total - max : 0;
            var result = new T[total - skip];
            var fromFirst = first[Math.Min(skip, first.Length)..];
            fromFirst.CopyTo(result);
            second[Math.Max(0, skip - first.Length)..].CopyTo(result.AsSpan(fromFirst.Length));
            return result;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)ToArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static ReadOnlySpan<T> Slice(ReadOnlySpan<T> segment, Interval<T> interval, IComparer<T>? comparer)
    {
        var from = interval.From is T low ? segment.LowerBound(low, comparer) : 0;
        var to = interval.To is T high ? segment.LowerBound(high, comparer) : segment.Length;
        return segment[from..Math.Max(from, to)];
    }

    // The ring is at most two runs of the array: [start, end) or [start, length) + [0, end).
    private ReadOnlySpan<T> FirstSegment()
        => _count == 0 ? [] : _start < _end ? _items.AsSpan(_start, _end - _start) : _items.AsSpan(_start);

    private ReadOnlySpan<T> SecondSegment()
        => _count == 0 || _start < _end ? [] : _items.AsSpan(0, _end);

    private int Physical(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
        return (_start + index) % _items.Length;
    }

    private int Next(int index) => index + 1 == _items.Length ? 0 : index + 1;

    private int Previous(int index) => (index == 0 ? _items.Length : index) - 1;

    private void ThrowIfEmpty()
    {
        if (_count == 0)
            throw new InvalidOperationException("The buffer is empty");
    }
}
