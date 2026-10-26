using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class SeriesKeysTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_FollowTheClock_WhenTheTimeAdvances()
    {
        var clock = new FakeTimeProvider(Start);
        var keys = new TimeKeys(clock);

        var first = keys.NextKey();
        clock.Advance(TimeSpan.FromSeconds(1.5));
        var second = keys.NextKey();

        first.Should().Be(Start.UtcDateTime);
        first.Kind.Should().Be(DateTimeKind.Utc);
        second.Should().Be(Start.UtcDateTime.AddSeconds(1.5));
        keys.Now.Should().Be(second);
    }

    [Fact]
    public void Should_MeasureSeconds_WhenTimeKeysCompareTwoKeys()
    {
        var keys = new TimeKeys(new FakeTimeProvider(Start));
        var t = Start.UtcDateTime;

        keys.Distance(t, t.AddMilliseconds(250)).Should().Be(0.25);
        keys.Distance(t.AddSeconds(1), t).Should().Be(-1);
    }

    [Fact]
    public void Should_CountFromTheStart_WhenIndexKeysAreAsked()
    {
        var keys = new IndexKeys(5);

        new[] { keys.NextKey(), keys.NextKey(), keys.NextKey() }.Should().Equal(5L, 6L, 7L);
        keys.Distance(3, 10).Should().Be(7);
    }

    [Fact]
    public void Should_GiveUniqueKeys_WhenManyThreadsAsk()
    {
        var keys = new IndexKeys();
        var taken = new ConcurrentBag<long>();

        Parallel.For(0, 8, _ =>
        {
            for (var i = 0; i < 1000; i++)
                taken.Add(keys.NextKey());
        });

        taken.Order().Should().Equal(Enumerable.Range(0, 8000).Select(i => (long)i));
    }
}
