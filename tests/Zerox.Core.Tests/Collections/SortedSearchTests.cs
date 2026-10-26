using System.Runtime.InteropServices;
using Zerox.Core.Collections;

namespace Zerox.Core.Tests.Collections;

public sealed class SortedSearchTests
{
    private static readonly int[] Sample = [1, 2, 2, 2, 5];

    [Fact]
    public void Should_ReturnZero_WhenTheSequenceIsEmpty()
    {
        int[] empty = [];

        empty.LowerBound(3).Should().Be(0);
        empty.UpperBound(3).Should().Be(0);
        empty.BinarySearchFirst(3).Should().Be(~0);
        empty.BinarySearchLast(3).Should().Be(~0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 4)]
    [InlineData(5, 4)]
    [InlineData(9, 5)]
    public void Should_PointAtTheFirstNotLess_WhenLowerBound(int value, int expected)
        => Sample.LowerBound(value).Should().Be(expected);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    [InlineData(5, 5)]
    [InlineData(9, 5)]
    public void Should_PointAfterTheEqualRun_WhenUpperBound(int value, int expected)
        => Sample.UpperBound(value).Should().Be(expected);

    [Fact]
    public void Should_FindTheFirstAndTheLastEqual_WhenTheValueIsPresent()
    {
        Sample.BinarySearchFirst(2).Should().Be(1);
        Sample.BinarySearchLast(2).Should().Be(3);
        Sample.BinarySearchFirst(1).Should().Be(0);
        Sample.BinarySearchLast(1).Should().Be(0);
        Sample.BinarySearchFirst(5).Should().Be(4);
        Sample.BinarySearchLast(5).Should().Be(4);
    }

    [Fact]
    public void Should_ReturnTheComplementOfTheInsertionPoint_WhenTheValueIsAbsent()
    {
        Sample.BinarySearchFirst(3).Should().Be(~4);
        Sample.BinarySearchLast(3).Should().Be(~4);
        Sample.BinarySearchFirst(0).Should().Be(~0);
        Sample.BinarySearchLast(9).Should().Be(~5);
    }

    [Fact]
    public void Should_UseTheComparer_WhenOneIsGiven()
    {
        int[] descending = [5, 3, 3, 1];
        var comparer = Comparer<int>.Create((a, b) => b.CompareTo(a));

        descending.LowerBound(3, comparer).Should().Be(1);
        descending.UpperBound(3, comparer).Should().Be(3);
    }

    [Fact]
    public void Should_SearchArraysSpansAndLists_WhenCalledOnThem()
    {
        var array = Sample.ToArray();
        Span<int> span = array;
        var list = Sample.ToList();
        var listSpan = CollectionsMarshal.AsSpan(list);

        array.LowerBound(2).Should().Be(1);
        span.LowerBound(2).Should().Be(1);
        listSpan.LowerBound(2).Should().Be(1);
        array.UpperBound(2).Should().Be(4);
        span.UpperBound(2).Should().Be(4);
        listSpan.UpperBound(2).Should().Be(4);
    }

    [Fact]
    public void Should_AgreeWithALinearScan_WhenTheDataIsRandom()
    {
        var random = new Random(42);
        for (var round = 0; round < 1000; round++)
        {
            var data = Enumerable.Range(0, random.Next(51)).Select(_ => random.Next(21)).Order().ToArray();
            for (var value = -1; value <= 21; value++)
            {
                var lower = LinearFirst(data, x => x >= value);
                var upper = LinearFirst(data, x => x > value);

                data.LowerBound(value).Should().Be(lower);
                data.UpperBound(value).Should().Be(upper);
                data.BinarySearchFirst(value).Should().Be(lower < data.Length && data[lower] == value ? lower : ~lower);
                data.BinarySearchLast(value).Should().Be(upper > 0 && data[upper - 1] == value ? upper - 1 : ~upper);
            }
        }
    }

    private static int LinearFirst(int[] data, Func<int, bool> predicate)
    {
        var index = Array.FindIndex(data, x => predicate(x));
        return index < 0 ? data.Length : index;
    }
}
