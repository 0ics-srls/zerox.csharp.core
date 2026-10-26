using Zerox.Core.Collections;

namespace Zerox.Core.Tests.Collections;

public sealed class IntervalTests
{
    [Fact]
    public void Should_BuildTheWindow_WhenUsingTheFactories()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Interval.Range(1, 5).Should().Be(new Interval<int>(1, 5, null));
        Interval.Since(3, 2).Should().Be(new Interval<int>(3, null, 2));
        Interval.LastN<int>(4).Should().Be(new Interval<int>(null, null, 4));
        Interval.Last(TimeSpan.FromSeconds(10), now).Should().Be(new Interval<DateTime>(now.AddSeconds(-10), null, null));
    }

    [Fact]
    public void Should_OrderByKey_WhenComparingPairs()
    {
        var comparer = KeyComparer.For<int, string>();

        comparer.Compare(new(2, "a"), new(1, "z")).Should().BePositive();
        comparer.Compare(new(1, "a"), new(1, "b")).Should().Be(0);
        KeyComparer.For<int, string>().Should().BeSameAs(comparer);
    }
}
