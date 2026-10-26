using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class CounterWideningTests
{
    [Fact]
    public void TestLongSeriesWithShortInputSeries()
    {
        var s = new ArraySeries<DateTime, long>("Prova", 10, new TimeKeys(new FakeTimeProvider()), 100);
        var values = new ushort[10];

        for (var i = 0; i < 10; ++i)
            values[i] = 0xfffe;
        s.AddCounters(values);
        Assert.DoesNotContain(s.Last, l => l > 0xffff);

        for (var i = 0; i < 10; ++i)
            values[i] += 1;
        s.AddCounters(values);
        Assert.Equal(10, s.Last.Count(l => l == 0xffff));

        for (var i = 0; i < 10; ++i)
            values[i] += 1;
        s.AddCounters(values);
        Assert.Equal(10, s.Last.Count(l => l == 0x10000));

        s.Clear();
        long cnt = 0;
        values = new ushort[10];
        s.AddCounters(values);
        var last = s.Last.ToArray();
        for (var j = 1; j < 1000; ++j)
        {
            cnt += 20000;
            for (var i = 0; i < 10; ++i)
                values[i] += 20000; // overruns many times
            s.AddCounters(values);
            Assert.Equal(10, s.Last.Where((l, i) => l - last[i] == 20000).Count());
            last = s.Last.ToArray();
        }
        Assert.Equal(10, s.Last.Count(l => l == cnt));
    }

    [Fact]
    public void Should_WidenA16BitCounter_WhenItWraps()
    {
        CounterWidening.Widen((ushort)5, 0x1_FFF0).Should().Be(0x2_0005);
        CounterWidening.Widen((ushort)0xFFF5, 0x1_FFF0).Should().Be(0x1_FFF5);
    }

    [Fact]
    public void Should_WidenA32BitCounter_WhenItWraps()
    {
        CounterWidening.Widen(5u, 0x1_FFFF_FFF0).Should().Be(0x2_0000_0005);

        var series = new ArraySeries<long, long>("uint", 1, new IndexKeys());
        series.AddCounters(new uint[] { 0xFFFF_FFFF });
        series.AddCounters(new uint[] { 1 });

        series.Last.Should().Equal(0x1_0000_0001L);
    }
}
