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
    public void CB_BaseTest()
    {
        var cb = new CircularBuffer<CBElement>(10);
        var removed = new List<CBElement>();
        for (var i = 0; i < 15; ++i)
        {
            if (cb.PushBack(new CBElement(i, i), out var o))
                removed.Add(o);
        }
        Assert.Equal("0,1,2,3,4", Dump(removed));
        Assert.Equal("5,6,7,8,9,10,11,12,13,14", Dump(cb));

        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i * 2, i * 2), out _);
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb));

        cb.Clear();
        removed.Clear();
        for (var i = 0; i < 15; ++i)
        {
            if (cb.PushBack(new CBElement(i, i), out var o))
                removed.Add(o);
        }
        Assert.Equal("0,1,2,3,4", Dump(removed));
    }

    [Fact]
    public void CB_BaseReverseTest()
    {
        var cb = new CircularBuffer<CBElement>(10);
        for (var i = 0; i < 15; ++i)
            cb.PushFront(new CBElement(i, i), out _);
        Assert.Equal("14,13,12,11,10,9,8,7,6,5", Dump(cb));

        for (var i = 0; i < 15; ++i)
            cb.PushFront(new CBElement(i * 2, i * 2), out _);
        Assert.Equal("28,26,24,22,20,18,16,14,12,10", Dump(cb));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(100, 100000)]
    public void CB_BaseTestHudge(int size, int steps)
    {
        var cb = new CircularBuffer<CBElement>(size);
        for (var i = 0; i < steps; ++i)
            cb.PushBack(new CBElement(i * 2, i * 2), out _);

        var chk = new int[size];
        for (var i = 0; i < size; ++i)
            chk[i] = (steps * 2 - size * 2) + i * 2;
        Assert.Equal(string.Join(",", chk), Dump(cb));
    }

    [Fact]
    public void CB_BaseViewTest()
    {
        var cb = new CircularBuffer<CBElement>(10);
        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i, i), out _);

        // the ring is cycled, internally "10,11,12,13,14,  5,6,7,8,9"
        Assert.Equal("5,6,7,8,9,10,11,12,13,14", Dump(cb));
        Assert.Equal("5,6,7,8,9,10,11,12,13,14", Dump(cb.View(Points(2, 15), ByKey)));
        Assert.Equal("10,11,12,13,14", Dump(cb.View(Points(10, 15), ByKey)));
        Assert.Equal("10,11,12,13,14", Dump(cb.View(Points(10, int.MaxValue), ByKey)));
        Assert.Equal("5,6,7,8,9", Dump(cb.View(Points(2, 10), ByKey)));
        Assert.Equal("5,6,7,8,9", Dump(cb.View(Points(int.MinValue, 10), ByKey)));

        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i * 2, i * 2));
        Assert.Equal("", Dump(cb.View(Points(int.MinValue, 10), ByKey)));
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb));
    }

    [Fact]
    public void CB_DateTimeViewTest()
    {
        var byTime = KeyComparer.For<DateTime, int>();
        var cb = new CircularBuffer<KeyValuePair<DateTime, int>>(1000);
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nowInsert = now.AddMicroseconds(500);
        for (var i = 0; i < 200; ++i)
        {
            cb.PushBack(new(nowInsert.AddMilliseconds(i), i));
            var current = now.AddMilliseconds(i + 1);

            var view = cb.View(ToPoints(Interval.Last(TimeSpan.FromMilliseconds(100), current)), byTime);
            Assert.Equal(Math.Min(i + 1, 100), view.Length);

            var view1 = cb.View(ToPoints(Interval.Last(TimeSpan.FromMilliseconds(100), current, 10)), byTime);
            Assert.Equal(Math.Min(i + 1, 10), view1.Length);

            var rev = view.Reverse().ToArray();
            var rev1 = view1.Reverse().ToArray();
            for (var j = 0; j < rev1.Length; ++j)
                Assert.Equal(rev[j].Key, rev1[j].Key);
        }

        static Interval<KeyValuePair<DateTime, int>> ToPoints(Interval<DateTime> interval)
            => new(interval.From is DateTime from ? new(from, 0) : null, interval.To is DateTime to ? new(to, 0) : null, interval.MaxLastCount);
    }

    [Fact]
    public void CB_BaseViewSparseTest()
    {
        var cb = new CircularBuffer<CBElement>(10);
        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i * 2, i * 2));

        // the ring is cycled, internally "20,22,24,26,28,  10,12,14,16,18"
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb));
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb.View(Points(9, 29), ByKey)));
        Assert.Equal("12,14,16,18", Dump(cb.View(Points(11, 19), ByKey)));
        Assert.Equal("22,24,26", Dump(cb.View(Points(21, 27), ByKey)));
        Assert.Equal("16,18,20", Dump(cb.View(Points(15, 22), ByKey)));
        Assert.Equal("", Dump(cb.View(Points(10, 5), ByKey)));

        for (var i = 0; i < 15; ++i)
            cb.PushBack(new CBElement(i * 2, i * 2));
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb));
        Assert.Equal("22,24,26,28", Dump(cb.View(Interval.LastN<CBElement>(4), ByKey)));
        Assert.Equal("10,12,14,16,18,20,22,24,26,28", Dump(cb.View(Interval.LastN<CBElement>(40), ByKey)));
        Assert.Equal("", Dump(cb.View(Interval.LastN<CBElement>(0), ByKey)));
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
        }, TestContext.Current.CancellationToken);
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

    [Fact]
    public void Should_RejectTheCapacity_WhenItIsLessThanOne()
    {
        FluentActions.Invoking(() => new CircularBuffer<int>(0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new CircularBuffer<int>(2, [1, 2, 3])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_StartWithTheItems_WhenConstructedWithItems()
    {
        var partial = new CircularBuffer<int>(3, [1, 2]);
        partial.ToArray().Should().Equal(1, 2);
        partial.IsFull.Should().BeFalse();

        var full = new CircularBuffer<int>(3, [1, 2, 3]);
        full.IsFull.Should().BeTrue();
        full.PushBack(4);
        full.ToArray().Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRange_WhenTheIndexIsOutside()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2]);

        FluentActions.Invoking(() => buffer[2]).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => buffer[-1]).Should().Throw<ArgumentOutOfRangeException>();
        buffer[1] = 9;
        buffer.ToArray().Should().Equal(1, 9);
    }

    [Fact]
    public void Should_ReturnThePoppedItem_WhenPoppingFromBothEnds()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);

        buffer.PopFront().Should().Be(1);
        buffer.PopBack().Should().Be(3);
        buffer.ToArray().Should().Equal(2);
        buffer.Front().Should().Be(2);
        buffer.Back().Should().Be(2);
    }

    [Fact]
    public void Should_ThrowInvalidOperation_WhenTheBufferIsEmpty()
    {
        var buffer = new CircularBuffer<int>(3);

        FluentActions.Invoking(() => buffer.PopBack()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => buffer.PopFront()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => buffer.Front()).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => buffer.Back()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_KeepTheView_WhenTheBufferChangesAfterwards()
    {
        var cb = new CircularBuffer<CBElement>(10);
        for (var i = 0; i < 10; ++i)
            cb.PushBack(new CBElement(i, i));

        var view = cb.View(Points(3, 7), ByKey);
        for (var i = 100; i < 120; ++i)
            cb.PushBack(new CBElement(i, i));

        Dump(view).Should().Be("3,4,5,6");
    }

    [Fact]
    public void Should_RejectTheMaxLastCount_WhenItIsNegative()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2]);

        FluentActions.Invoking(() => buffer.View(Interval.LastN<int>(-1))).Should().Throw<ArgumentOutOfRangeException>();
    }
}
