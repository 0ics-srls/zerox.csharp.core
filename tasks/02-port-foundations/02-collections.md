# Collections

<block id="02-sorted-search">
## TDDAB-2: Write the One Sorted Search on Spans

<intro>
Delivers lower bound, upper bound, first and last binary search over a sorted sequence, written
ONCE on `ReadOnlySpan<T>`. C# 14 applies an extension on `ReadOnlySpan<T>` to arrays and
`Span<T>` receivers; a `List<T>` is searched through `CollectionsMarshal.AsSpan(list)`.

Capability: the old code has it four times (`../../tmp/zerox/Zerox.Wpf.Core/Core/Collections/ArrayExtensions.cs`,
`ArraySegmentExtensions.cs`, `ListExtensions.cs`, `SortedListExtensions.cs`), without tests and
with two bugs: an empty input returned -1, and `UpperBound` of a value equal to the first element
returned 0 instead of the index after the equal run. .NET's `MemoryExtensions.BinarySearch`
returns an arbitrary equal index and has no bounds. This block writes the single implementation
with the standard semantics (as C++ `lower_bound` / `upper_bound`); nothing else in the
repository searches a sorted sequence.

File: `src/Zerox.Core/Collections/SortedSearch.cs`; tests
`tests/Zerox.Core.Tests/Collections/SortedSearchTests.cs`.
</intro>

<red>
- test: Should_ReturnZero_WhenTheSequenceIsEmpty — `LowerBound`, `UpperBound` on an empty span return 0; `BinarySearchFirst`, `BinarySearchLast` return `~0`
- test: Should_PointAtTheFirstNotLess_WhenLowerBound — on `[1, 2, 2, 2, 5]`: LowerBound(0) = 0, LowerBound(1) = 0, LowerBound(2) = 1, LowerBound(3) = 4, LowerBound(5) = 4, LowerBound(9) = 5
- test: Should_PointAfterTheEqualRun_WhenUpperBound — on `[1, 2, 2, 2, 5]`: UpperBound(0) = 0, UpperBound(1) = 1, UpperBound(2) = 4, UpperBound(3) = 4, UpperBound(5) = 5, UpperBound(9) = 5
- test: Should_FindTheFirstAndTheLastEqual_WhenTheValueIsPresent — on `[1, 2, 2, 2, 5]`: BinarySearchFirst(2) = 1, BinarySearchLast(2) = 3, both = 0 for 1, both = 4 for 5
- test: Should_ReturnTheComplementOfTheInsertionPoint_WhenTheValueIsAbsent — on `[1, 2, 2, 2, 5]`: BinarySearchFirst(3) = ~4, BinarySearchLast(3) = ~4, BinarySearchFirst(0) = ~0, BinarySearchLast(9) = ~5
- test: Should_UseTheComparer_WhenOneIsGiven — on `[5, 3, 3, 1]` with a descending comparer: LowerBound(3) = 1, UpperBound(3) = 3
- test: Should_SearchArraysSpansAndLists_WhenCalledOnThem — the same answers from an `int[]`, a `Span<int>` and `CollectionsMarshal.AsSpan(List<int>)`
- test: Should_AgreeWithALinearScan_WhenTheDataIsRandom — 1000 seeded random sorted arrays (length 0–50, values 0–20): every bound equals the index found by a linear scan
</red>

### Implementation

```csharp
namespace Zerox.Core.Collections;

// Searches over a sorted sequence (ascending for the comparer). One implementation for arrays,
// spans and lists (List<T> through CollectionsMarshal.AsSpan).
public static class SortedSearch
{
    // Index of the first element not less than value; Length when there is none.
    public static int LowerBound<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (comparer.Compare(sorted[middle], value) < 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    // Index of the first element greater than value; Length when there is none.
    public static int UpperBound<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (comparer.Compare(sorted[middle], value) <= 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    // Index of the first element equal to value, or the bitwise complement of where it would go.
    public static int BinarySearchFirst<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var index = sorted.LowerBound(value, comparer);
        return index < sorted.Length && comparer.Compare(sorted[index], value) == 0 ? index : ~index;
    }

    // Index of the last element equal to value, or the bitwise complement of where it would go.
    public static int BinarySearchLast<T>(this ReadOnlySpan<T> sorted, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var index = sorted.UpperBound(value, comparer);
        return index > 0 && comparer.Compare(sorted[index - 1], value) == 0 ? index - 1 : ~index;
    }
}
```

Test shape:

```csharp
namespace Zerox.Core.Tests.Collections;

public sealed class SortedSearchTests
{
    private static readonly int[] Sample = [1, 2, 2, 2, 5];

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(2, 4)] [InlineData(3, 4)] [InlineData(5, 5)] [InlineData(9, 5)]
    public void Should_PointAfterTheEqualRun_WhenUpperBound(int value, int expected)
        => Sample.UpperBound(value).Should().Be(expected);

    [Fact]
    public void Should_AgreeWithALinearScan_WhenTheDataIsRandom()
    {
        var random = new Random(42);
        for (var round = 0; round < 1000; round++)
        {
            var data = Enumerable.Range(0, random.Next(51)).Select(_ => random.Next(21)).Order().ToArray();
            for (var value = -1; value <= 21; value++)
            {
                data.LowerBound(value).Should().Be(LinearFirst(data, x => x >= value));
                data.UpperBound(value).Should().Be(LinearFirst(data, x => x > value));
            }
        }
    }

    private static int LinearFirst(int[] data, Func<int, bool> predicate)
    {
        var index = Array.FindIndex(data, x => predicate(x));
        return index < 0 ? data.Length : index;
    }
}
```

<success>
- [ ] `SortedSearchTests` pass (`dotnet test --project tests/Zerox.Core.Tests -- --filter-class "Zerox.Core.Tests.Collections.SortedSearchTests"`)
- [ ] `src/Zerox.Core/Collections/SortedSearch.cs` is the only file of the repository with a binary search loop
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="03-bit-mask">
## TDDAB-3: Port the Bit Mask of Arbitrary Length

<intro>
Delivers `BitMask`: a mask of any number of bits stored in `ulong` words, with get, set, reset,
population count (`BitOperations.PopCount`), the list of set bits, AND, OR and value equality.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Collections/BitArray.cs` (tests
`../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Collections/BitArrayTest.cs`, NUnit). .NET's
`System.Collections.BitArray` has no fast population count and its name clashes, so the type is
`BitMask`. Bugs fixed: `==` wrote into both operands (it masked their last word in place) and its
mask kept the unused high bits instead of the used ones — the unused bits are always 0 (only
`Set` writes bits, and only inside the size), so equality compares the words as they are; the
old tests caught an exception in `try/catch` and passed when nothing was thrown; out of range was
`IndexOutOfRangeException` (reserved) and is now `ArgumentOutOfRangeException`; AND/OR of
different sizes is an `ArgumentException`.

Files: `src/Zerox.Core/Collections/BitMask.cs`; tests
`tests/Zerox.Core.Tests/Collections/BitMaskTests.cs` (the old five tests, NUnit → xunit, with
their names, `Assert.Throws` instead of `try/catch`, plus the new ones).
</intro>

<red>
- test: Test_Count — masks `a` {10,20,30,40,50} and `b` {10,30,50,70,90} of 100 bits count 5 each; `Set(101)` and `Set(-1)` throw `ArgumentOutOfRangeException`
- test: Test_Get — `a.Get` is true for 10..50 step 10 and false for 0; `Get(101)`, `Get(-1)` throw `ArgumentOutOfRangeException`
- test: Test_Reset — resetting the five bits of `a` leaves count 0; `Reset(101)`, `Reset(-1)` throw `ArgumentOutOfRangeException`
- test: Test_Logic — `(a | b).Count` is 7, `(a & b).Count` is 3, `a == a`, `a != b`
- test: Test_IndexList — `a.GetSetBits()` is `[10, 20, 30, 40, 50]`
- test: Should_LeaveTheOperandsUnchanged_WhenCompared — after `a == b` and `a | b`, both masks still hold exactly their bits
- test: Should_BeEqual_WhenTwoMasksHoldTheSameBits — two 100-bit masks with the same bits are equal (`==`, `Equals`, same `GetHashCode`); masks of different sizes are not equal
- test: Should_CountAcrossWords_WhenTheMaskIsLongerThan64Bits — a 200-bit mask with bits 0, 63, 64, 199 counts 4 and lists them in order
- test: Should_Throw_WhenCombiningMasksOfDifferentSizes — `a | new BitMask(64)` and `a & new BitMask(64)` throw `ArgumentException`
</red>

### Implementation

```csharp
namespace Zerox.Core.Collections;

// A mask of any number of bits. Bits outside Size are never set, so the words compare as they are.
public sealed class BitMask : IEquatable<BitMask>
{
    private readonly ulong[] _words;

    public BitMask(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        Size = size;
        _words = new ulong[(size + 63) / 64];
    }

    public int Size { get; }

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var word in _words)
                count += BitOperations.PopCount(word);
            return count;
        }
    }

    public bool Get(int index) { CheckIndex(index); return (_words[index >> 6] & (1UL << (index & 63))) != 0; }
    public void Set(int index) { CheckIndex(index); _words[index >> 6] |= 1UL << (index & 63); }
    public void Reset(int index) { CheckIndex(index); _words[index >> 6] &= ~(1UL << (index & 63)); }

    public IReadOnlyList<int> GetSetBits() { ... }   // ascending indexes of the set bits

    public static BitMask operator |(BitMask left, BitMask right) => Combine(left, right, static (a, b) => a | b);
    public static BitMask operator &(BitMask left, BitMask right) => Combine(left, right, static (a, b) => a & b);
    public static bool operator ==(BitMask? left, BitMask? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(BitMask? left, BitMask? right) => !(left == right);

    public bool Equals(BitMask? other) => other is not null && Size == other.Size && _words.AsSpan().SequenceEqual(other._words);
    public override bool Equals(object? obj) => obj is BitMask other && Equals(other);
    public override int GetHashCode() { var hash = new HashCode(); hash.Add(Size); foreach (var word in _words) hash.Add(word); return hash.ToHashCode(); }

    private void CheckIndex(int index) => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Size, nameof(index));

    private static BitMask Combine(BitMask left, BitMask right, Func<ulong, ulong, ulong> operation)
    {
        if (left.Size != right.Size)
            throw new ArgumentException($"Masks of different sizes: {left.Size} and {right.Size}", nameof(right));
        var result = new BitMask(left.Size);
        for (var i = 0; i < left._words.Length; i++)
            result._words[i] = operation(left._words[i], right._words[i]);
        return result;
    }
}
```

If the analyzers ask for named alternatives of the operators (CA2225), add `BitwiseOr` and
`BitwiseAnd` calling the same `Combine`.

<success>
- [ ] `BitMaskTests` pass (9 tests)
- [ ] `BitMask` never writes into an operand outside `Set`/`Reset` (the operands test is green)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>

<block id="04-circular-linked-list">
## TDDAB-4: Port the Circular Linked List

<intro>
Delivers `CircularLinkedList<T>`: a set of unique items in insertion order whose `Next()` walks
round and round (round-robin), with O(1) add, remove and lookup through an index.

Capability: the old `../../tmp/zerox/Zerox.Wpf.Core/Core/Collections/LinkedList/LinkedList.cs`
(tests `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Collections/LinkedList/CircularLinkedListTest.cs`,
NUnit, five tests). Changes: the node type becomes `internal sealed` (the old public
`LinkedListNode<DT>` clashed with the BCL type); `Add` throws `ArgumentException` on a duplicate by
calling `TryAdd` (one insertion path, DRY); `Count` is an `int`; `Remove` returns whether it
removed. Behaviour otherwise unchanged: `Peek()` and `Next()` give `null` on an empty list, a
removed current item moves the cursor to the next one, `Reset()` goes back to the head, the
enumeration starts at the head.

Files: `src/Zerox.Core/Collections/CircularLinkedList.cs`; tests
`tests/Zerox.Core.Tests/Collections/CircularLinkedListTests.cs` (old names; the old `SomeData`
class with value equality and the `SomeCList` subclass become a `private sealed record SomeData(int Val)`
and the list is used directly).
</intro>

<red>
- test: Test_Exception — adding an item equal to one already in the list throws `ArgumentException`
- test: Test_TryAdd — `TryAdd` of a duplicate returns false, of a new item true
- test: Test_limits — remove on an empty list does nothing; `Peek()` and `Next()` are null on an empty list; with one item `Next()` returns it every time; after adding 0..999, `Next()` returns 0..999 in order and then `Peek()` is 0 again
- test: Test_Enum — enumerating 1000 added items gives them in insertion order
- test: Test_Create — removing the 1000 items one by one brings `Count` from 1000 to 0
- test: Should_MoveTheCursorToTheNextItem_WhenTheCurrentItemIsRemoved — on {1, 2, 3} with the cursor on 2, removing 2 makes `Peek()` 3
- test: Should_ReturnFalse_WhenRemovingAnAbsentItem — `Remove` of an absent item returns false and `Count` is unchanged
</red>

### Implementation

```csharp
namespace Zerox.Core.Collections;

// Unique items in insertion order; Next() walks round and round. Add, Remove, Contains are O(1).
public sealed class CircularLinkedList<T> : IEnumerable<T>
    where T : class
{
    private readonly Dictionary<T, Node> _index = [];
    private Node? _head;
    private Node? _current;

    public int Count => _index.Count;

    public bool Contains(T item) => _index.ContainsKey(item);

    public void Add(T item)
    {
        if (!TryAdd(item))
            throw new ArgumentException("The item is already in the list", nameof(item));
    }

    public bool TryAdd(T item)
    {
        var node = new Node(item);
        if (!_index.TryAdd(item, node))
            return false;
        if (_head is null)
        {
            _head = _current = node.Next = node.Previous = node;
        }
        else
        {
            node.Next = _head;
            node.Previous = _head.Previous;
            _head.Previous!.Next = node;
            _head.Previous = node;
        }
        return true;
    }

    public bool Remove(T item) { ... }   // as the old Remove, returning whether it removed
    public void Clear() { _head = _current = null; _index.Clear(); }
    public T? Peek() => _current?.Value;
    public T? Next() { ... }             // returns the current value and advances
    public void Reset() => _current = _head;
    public IEnumerator<T> GetEnumerator() { ... }   // from the head, once round
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Node(T value)
    {
        public T Value { get; } = value;
        public Node? Previous { get; set; }
        public Node? Next { get; set; }
    }
}
```

<success>
- [ ] `CircularLinkedListTests` pass (7 tests)
- [ ] No public node type in `Zerox.Core.Collections`
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
