# Collections

<block id="01-interval-and-circular-buffer">
## TDDAB-1: Port the Circular Buffer with Interval Views

<intro>
Delivers three general collection pieces, used by every series of this stage:
- `Interval<T>`: a window over a sorted sequence — `From` inclusive, `To` exclusive (null =
  unbounded), then only the last `MaxLastCount` elements of what falls inside; factories on the
  non-generic `Interval`: `Range(from, to, maxLastCount?)`, `Since(from, maxLastCount?)`,
  `LastN(count)`, `Last(span, now, maxLastCount?)` (a `DateTime` window ending at `now`).
- `CircularBuffer<T>` (`T : struct`): fixed capacity; `PushBack` / `PushFront` (when full the
  overwritten item is returned through `out` and the method returns true); `PopBack` /
  `PopFront` return the item; indexer, `Front`, `Back`, `Count`, `Capacity`, `IsEmpty`,
  `IsFull`, `Clear`, `ToArray`; `View(interval, comparer?)` returns a COPY of the elements inside
  the interval, the buffer being sorted ascending for the comparer. Thread safe: one `Lock`.
- `KeyComparer.For<TKey, TValue>()`: orders `KeyValuePair<TKey, TValue>` by key (cached).

Capability: old `../../tmp/zerox/Zerox.Wpf.Core/Core/Data/CircularBuffer.cs` and six interval
types in `../../tmp/zerox/Zerox.Wpf.Core/Core/Collections/IInterval.cs`; tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Data/CircularBufferTest.cs` (NUnit, 7 tests). The old
tests pin the half-open semantics: on the keys `5..14`, `(2, 10)` gives `5..9` and `(10, 15)`
gives `10..14`. Bounds use stage A's `SortedSearch.LowerBound` on each of the two segments of the
ring (from = LowerBound(From), to = LowerBound(To)). Bugs fixed: the old view was lazy and kept a
pooled interval that the caller had already given back to the pool (a copy now; no pool:
`Interval<T>` is a struct); the indexer threw `IndexOutOfRangeException` (now
`ArgumentOutOfRangeException`); `PopBack`/`PopFront` checked emptiness outside the lock. The old
static test hook `TestUpperLimitGenerator` and `DateTime.Now` disappear: `Interval.Last` takes
`now`.

Ported tests keep their names; `CBElement` is `KeyValuePair<int, double>` (the old alias) and a
view of it uses `KeyComparer.For<int, double>()`; the old `DoubleIntSeriesInterval(a, b)` becomes
`Points(a, b)` = `new Interval<KeyValuePair<int, double>>(new(a, 0), new(b, 0))`.
`CB_MultiThreadTest` loses its `Thread.Sleep` (a writer task pushes until the reader is done).

Files: `src/Zerox.Core/Collections/Interval.cs`, `src/Zerox.Core/Collections/CircularBuffer.cs`,
`src/Zerox.Core/Collections/KeyComparer.cs`; tests
`tests/Zerox.Core.Tests/Collections/CircularBufferTests.cs`,
`tests/Zerox.Core.Tests/Collections/IntervalTests.cs`.
</intro>

<red>
- test: CB_BaseTest — capacity 10, 15 `PushBack(item, out removed)`: the removed items are `0,1,2,3,4`, the buffer is `5..14`; 15 more pushes of `i * 2` give `10,12,…,28`; after `Clear` the removed items are again `0,1,2,3,4`
- test: CB_BaseReverseTest — 15 `PushFront` give `14,13,…,5`; 15 more of `i * 2` give `28,26,…,10`
- test: CB_BaseTestHudge — theory (10, 10) and (100, 100000): after `steps` pushes of `i * 2` the buffer holds the last `size` values in order
- test: CB_BaseViewTest — on `5..14` (wrapped ring): `(2,15)` all, `(10,15)` `10..14`, `(10,int.MaxValue)` `10..14`, `(2,10)` `5..9`, `(int.MinValue,10)` `5..9`; after 15 pushes of `i * 2`: `(int.MinValue,10)` empty and the buffer `10,12,…,28`
- test: CB_DateTimeViewTest — capacity 1000 of `KeyValuePair<DateTime, int>`, point `i` at `now + 0.5 ms + i ms`, current time `now + (i + 1) ms`: `Interval.Last(100 ms, current)` has `min(i + 1, 100)` points, with `maxLastCount: 10` `min(i + 1, 10)`, and the last points of the two views are the same
- test: CB_BaseViewSparseTest — on `10,12,…,28`: `(9,29)` all, `(11,19)` `12,14,16,18`, `(21,27)` `22,24,26`, `(15,22)` `16,18,20`, `(10,5)` empty; `LastN(4)` `22,24,26,28`, `LastN(40)` all, `LastN(0)` empty
- test: CB_MultiThreadTest @Integration — a writer task pushes increasing keys while the reader takes 10000 views of everything: each view has consecutive keys, at least one value was read
- test: Should_RejectTheCapacity_WhenItIsLessThanOne — `new CircularBuffer<int>(0)` throws `ArgumentOutOfRangeException`; 3 items for capacity 2 throw `ArgumentException`
- test: Should_StartWithTheItems_WhenConstructedWithItems — capacity 3 with `[1, 2]`: `ToArray()` is `[1, 2]`, `IsFull` false; with `[1, 2, 3]` `IsFull` true and the next `PushBack(4)` gives `[2, 3, 4]`
- test: Should_ThrowArgumentOutOfRange_WhenTheIndexIsOutside — on `[1, 2]`: `buffer[2]` and `buffer[-1]` throw `ArgumentOutOfRangeException`; `buffer[1] = 9` makes `[1, 9]`
- test: Should_ReturnThePoppedItem_WhenPoppingFromBothEnds — on `[1, 2, 3]`: `PopFront()` = 1, `PopBack()` = 3, the buffer is `[2]`, `Front()` = `Back()` = 2
- test: Should_ThrowInvalidOperation_WhenTheBufferIsEmpty — `PopBack`, `PopFront`, `Front`, `Back` on an empty buffer throw `InvalidOperationException`
- test: Should_KeepTheView_WhenTheBufferChangesAfterwards — a view taken, then 20 pushes: the view's elements are unchanged
- test: Should_RejectTheMaxLastCount_WhenItIsNegative — `View(Interval.LastN<int>(-1))` throws `ArgumentOutOfRangeException`
- test: Should_BuildTheWindow_WhenUsingTheFactories — `Range(1, 5)` = (1, 5, null); `Since(3, 2)` = (3, null, 2); `LastN<int>(4)` = (null, null, 4); `Last(10 s, now)` = (now − 10 s, null, null)
- test: Should_OrderByKey_WhenComparingPairs — `KeyComparer.For<int, string>()` orders `(2,"a")` after `(1,"z")`, `(1,"a")` equal to `(1,"b")`; two calls return the same instance
</red>

### Implementation

`src/Zerox.Core/Collections/Interval.cs`:

```csharp
namespace Zerox.Core.Collections;

// A window over a sorted sequence: From inclusive, To exclusive (null = unbounded), then only the
// last MaxLastCount elements of what falls inside.
public readonly record struct Interval<T>(T? From, T? To, int? MaxLastCount = null)
    where T : struct;

// The factories (static members live on a non-generic class, CA1000).
public static class Interval
{
    public static Interval<T> Range<T>(T from, T to, int? maxLastCount = null) where T : struct
        => new(from, to, maxLastCount);

    public static Interval<T> Since<T>(T from, int? maxLastCount = null) where T : struct
        => new(from, null, maxLastCount);

    public static Interval<T> LastN<T>(int count) where T : struct
        => new(null, null, count);

    // The time window of length span ending at now (the caller's clock).
    public static Interval<DateTime> Last(TimeSpan span, DateTime now, int? maxLastCount = null)
        => new(now - span, null, maxLastCount);
}
```

`src/Zerox.Core/Collections/KeyComparer.cs`:

```csharp
namespace Zerox.Core.Collections;

// Orders key/value pairs by key (a buffer of points sorted by key is searched with it).
public static class KeyComparer
{
    public static IComparer<KeyValuePair<TKey, TValue>> For<TKey, TValue>()
        where TKey : IComparable<TKey>
        => Cache<TKey, TValue>.Instance;

    private static class Cache<TKey, TValue>
        where TKey : IComparable<TKey>
    {
        public static readonly IComparer<KeyValuePair<TKey, TValue>> Instance =
            Comparer<KeyValuePair<TKey, TValue>>.Create(static (left, right) => left.Key.CompareTo(right.Key));
    }
}
```

`src/Zerox.Core/Collections/CircularBuffer.cs`:

```csharp
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
```

Test shape (`tests/Zerox.Core.Tests/Collections/CircularBufferTests.cs`):

```csharp
using System.Globalization;
using Zerox.Core.Collections;
using CBElement = System.Collections.Generic.KeyValuePair<int, double>;

namespace Zerox.Core.Tests.Collections;

public sealed class CircularBufferTests
{
    private static readonly IComparer<CBElement> ByKey = KeyComparer.For<int, double>();

    private static string Dump(IEnumerable<CBElement> items)
        => string.Join(",", items.Select(e => e.Value.ToString(CultureInfo.InvariantCulture)));

    private static Interval<CBElement> Points(int from, int to) => new(new(from, 0), new(to, 0));

    [Fact]
    public void CB_BaseViewTest()
    {
        var cb = new CircularBuffer<CBElement>(10);
        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i, i));

        Assert.Equal("5,6,7,8,9,10,11,12,13,14", Dump(cb));
        Assert.Equal("10,11,12,13,14", Dump(cb.View(Points(10, 15), ByKey)));
        Assert.Equal("5,6,7,8,9", Dump(cb.View(Points(2, 10), ByKey)));
        // … every case of the red line
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CB_MultiThreadTest()
    {
        var cb = new CircularBuffer<CBElement>(1000);
        var done = false;
        var writer = Task.Run(() =>
        {
            for (var i = 0; !Volatile.Read(ref done); ++i)
                cb.PushBack(new CBElement(i, i));
        });
        SpinWait.SpinUntil(() => cb.Count > 0);
        var read = 0;
        for (var j = 0; j < 10000; ++j)
        {
            var view = cb.View(Points(int.MinValue, int.MaxValue), ByKey);
            for (var k = 1; k < view.Length; ++k)
                Assert.Equal(view[k - 1].Key + 1, view[k].Key);
            read += view.Length;
        }
        Volatile.Write(ref done, true);
        await writer;
        Assert.True(read > 0);
    }
}
```

<success>
- [ ] `CircularBufferTests` and `IntervalTests` pass (16 tests, `CB_BaseTestHudge` with 2 cases)
- [ ] `CircularBuffer` throws no `IndexOutOfRangeException` (`grep -n "IndexOutOfRange" src/Zerox.Core/Collections/CircularBuffer.cs` is empty)
- [ ] No object pool and no `DateTime.Now` in `src/Zerox.Core/Collections` (`grep -rn -E "ObjectPool|DateTime.Now" src/Zerox.Core/Collections` is empty)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
