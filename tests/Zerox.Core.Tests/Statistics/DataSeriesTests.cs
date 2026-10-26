using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Collections;
using Zerox.Core.Identity;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class DataSeriesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class SteppingKeys(DateTime start, TimeSpan step) : ISeriesKeys<DateTime>
    {
        private DateTime _now = start;

        public DateTime NextKey() => _now += step;

        public double Distance(DateTime earlier, DateTime later) => (later - earlier).TotalSeconds;
    }

    // Keys from a fake clock that moves 1 ms after every key, so every key is distinct.
    private sealed class TickingKeys(FakeTimeProvider clock) : ISeriesKeys<DateTime>
    {
        private readonly TimeKeys _keys = new(clock);

        public DateTime NextKey()
        {
            var key = _keys.NextKey();
            clock.Advance(TimeSpan.FromMilliseconds(1));
            return key;
        }

        public double Distance(DateTime earlier, DateTime later) => _keys.Distance(earlier, later);
    }

    [Fact]
    public void TestRateOfSeries()
    {
        var s = new DataSeries<DateTime, double>("Prova", new SteppingKeys(Start.UtcDateTime, TimeSpan.FromSeconds(1)), 100);
        var rs = new DataSeries<DateTime, double>("ProvaRs", new SteppingKeys(Start.UtcDateTime, TimeSpan.FromSeconds(1)), 100);
        for (var i = 0; i < 100; i += 10)
        {
            s.Add(-i);
            if (i > 0)
            {
                Assert.Equal(-10, s.Rate, 1e-9);
                rs.Add(s.Rate);
            }
        }

        Assert.Equal(9, rs.Count);
        Assert.Equal(-10, rs.Mean, 1e-9);
    }

    [Fact]
    public void TestSeriesView()
    {
        var clock = new FakeTimeProvider(Start);
        var keys = new TimeKeys(clock);
        var sr16 = new DataSeries<DateTime, int>("SR1", keys);
        var start = keys.Now;
        for (var i = 0; i < 20; ++i)
        {
            if (i == 10)
                start = keys.Now;
            sr16.Add(i);
            clock.Advance(TimeSpan.FromMilliseconds(45));
        }
        var end = keys.Now;

        var view = sr16.View(Interval.Range(start, end));

        Assert.Equal(10, view.Length);
        Assert.True(view[0].Key >= start);
        Assert.True(view[^1].Key <= end);
    }

    [Fact]
    public void SeriesDataPersistorInt32Int()
    {
        var store = new Dictionary<string, KeyValuePair<long, int>[]>();
        var keys = new IndexKeys();
        var sr1 = new DataSeries<long, int>("SR1", keys, 10_000);
        var sr2 = new DataSeries<long, int>("SR2", keys, 10_000);
        for (var i = 0; i < 10_000; ++i)
        {
            sr1.Add(i);
            sr2.Add(i);
        }
        Assert.Equal(10_000, sr1.Count);
        Assert.Equal(10_000, sr2.Count);

        store[sr2.Name] = sr2.Snapshot();
        sr1.Clear();
        sr2.Clear();
        for (var i = 0; i < 10_000; ++i)
        {
            sr1.Add(1);
            sr2.Add(1);
        }
        sr2.Load(store[sr2.Name]);

        Assert.Equal(10_000, sr1.Count);
        Assert.Equal(10_000, sr2.Count);
        var r1 = sr1.View(Interval.LastN<long>(5000));
        var r2 = sr2.View(Interval.LastN<long>(5000));
        Assert.Equal(5000, r1.Length);
        Assert.Equal(5000, r2.Length);
        Assert.Equal(1, r1[0].Value);
        Assert.Equal(5000, r2[0].Value);
        Assert.Equal(1, r1[^1].Value);
        Assert.Equal(9999, r2[^1].Value);
        Assert.Equal(10_000, sr1.Sum);
        Assert.Equal(49_995_000, sr2.Sum);
        Assert.Equal(1, sr1.Min);
        Assert.Equal(0, sr2.Min);
        Assert.Equal(1, sr1.Max);
        Assert.Equal(9999, sr2.Max);
        Assert.True(r1[0].Key < r1[^1].Key);
        Assert.True(r2[0].Key < r2[^1].Key);
    }

    [Fact]
    public void SeriesDataPersistorDatetimeDouble()
    {
        var store = new Dictionary<string, KeyValuePair<DateTime, double>[]>();
        var keys = new TickingKeys(new FakeTimeProvider(Start));
        var sr1 = new DataSeries<DateTime, double>("SR1", keys, 10_000);
        var sr2 = new DataSeries<DateTime, double>("SR2", keys, 10_000);
        for (var i = 0; i < 10_000; ++i)
        {
            sr1.Add(i);
            sr2.Add(i);
        }
        Assert.Equal(10_000, sr1.Count);
        Assert.Equal(10_000, sr2.Count);

        store[sr2.Name] = sr2.Snapshot();
        sr1.Clear();
        sr2.Clear();
        for (var i = 0; i < 10_000; ++i)
        {
            sr1.Add(1);
            sr2.Add(1);
        }
        sr2.Load(store[sr2.Name]);

        var r1 = sr1.View(Interval.LastN<DateTime>(5000));
        var r2 = sr2.View(Interval.LastN<DateTime>(5000));
        Assert.Equal(10_000, sr1.Count);
        Assert.Equal(10_000, sr2.Count);
        Assert.Equal(5000, r1.Length);
        Assert.Equal(5000, r2.Length);
        Assert.Equal(1, r1[0].Value);
        Assert.Equal(5000, r2[0].Value);
        Assert.Equal(1, r1[^1].Value);
        Assert.Equal(9999, r2[^1].Value);
        Assert.Equal(10_000, sr1.Sum);
        Assert.Equal(49_995_000, sr2.Sum);
        Assert.Equal(1, sr1.Min);
        Assert.Equal(0, sr2.Min);
        Assert.Equal(1, sr1.Max);
        Assert.Equal(9999, sr2.Max);
        Assert.True(r1[0].Key < r1[^1].Key);
        Assert.True(r2[0].Key < r2[^1].Key);
    }

    [Fact]
    public void SeriesDataFirstLoadAndUpdate()
    {
        var store = new Dictionary<string, KeyValuePair<DateTime, double>[]>();
        var sr = new DataSeries<DateTime, double>("SR21", new TickingKeys(new FakeTimeProvider(Start)), 10_000);
        for (var i = 0; i < 10_000; ++i)
            sr.Add(i);

        store[sr.Name] = sr.Snapshot();
        sr.Load(store[sr.Name]);
        Assert.Equal(10_000, sr.Count);
        sr.Add(999);
        store[sr.Name] = sr.Snapshot();

        Assert.Equal(999, store[sr.Name][^1].Value);
    }

    [Fact]
    public void Should_ReportZero_WhenTheSeriesIsEmpty()
    {
        var series = new DataSeries<long, int>("empty", new IndexKeys());

        series.Count.Should().Be(0);
        series.Min.Should().Be(0);
        series.Max.Should().Be(0);
        series.Last.Should().Be(0);
        series.Sum.Should().Be(0);
        series.Mean.Should().Be(0);
        series.Rate.Should().Be(0);
        series.StoredCount.Should().Be(0);
        series.View(default).Should().BeEmpty();
    }

    [Fact]
    public void Should_StartTheRateAtZero_WhenTheFirstValueArrives()
    {
        var series = new DataSeries<long, int>("first", new IndexKeys());

        series.Add(50, 1000);

        series.Rate.Should().Be(0);
        series.Min.Should().Be(50);
        series.Max.Should().Be(50);
    }

    [Fact]
    public void Should_KeepTheFractionOfTheRate_WhenTheValuesAreIntegers()
    {
        var series = new DataSeries<long, int>("int", new IndexKeys());

        series.Add(10, 0);
        series.Add(11, 2);

        series.Rate.Should().Be(0.5);
    }

    [Fact]
    public void Should_ComputeANegativeRate_WhenAnUnsignedValueDecreases()
    {
        var series = new DataSeries<long, uint>("uint", new IndexKeys());

        series.Add(10u, 0);
        series.Add(4u, 1);

        series.Rate.Should().Be(-6);
    }

    [Fact]
    public void Should_KeepTheRate_WhenTwoValuesShareTheKey()
    {
        var series = new DataSeries<long, int>("same key", new IndexKeys());

        series.Add(0, 0);
        series.Add(5, 1);
        series.Add(9, 1);

        series.Rate.Should().Be(5);
        series.Last.Should().Be(9);
    }

    [Fact]
    public void Should_RejectTheKey_WhenItGoesBack()
    {
        var series = new DataSeries<long, int>("back", new IndexKeys());
        series.Add(1, 5);

        FluentActions.Invoking(() => series.Add(1, 4)).Should().Throw<ArgumentOutOfRangeException>();
        series.Count.Should().Be(1);
    }

    [Fact]
    public void Should_KeepOnlyTheCapacity_WhenMoreValuesArrive()
    {
        var series = new DataSeries<long, int>("small", new IndexKeys(), 3);
        for (var i = 1; i <= 5; i++)
            series.Add(i);

        series.StoredCount.Should().Be(3);
        series.Count.Should().Be(5);
        series.Get(0).Value.Should().Be(3);
        series.Get(2).Value.Should().Be(5);
        FluentActions.Invoking(() => series.Get(3)).Should().Throw<ArgumentOutOfRangeException>();
        series.Sum.Should().Be(15);
        series.Min.Should().Be(1);
    }

    [Fact]
    public void Should_SumTheHalfOpenRange_WhenViewSum()
    {
        var series = new DataSeries<long, int>("sum", new IndexKeys());
        for (var i = 0; i < 10; i++)
            series.Add(i);

        series.ViewSum(2, 5).Should().Be(9.0);
    }

    [Fact]
    public void Should_RejectThePoints_WhenLoadedKeysAreNotAscending()
    {
        var series = new DataSeries<long, int>("load", new IndexKeys());
        series.Add(7);
        series.Add(8);
        KeyValuePair<long, int>[] unordered = [new(1, 1), new(3, 3), new(2, 2)];

        FluentActions.Invoking(() => series.Load(unordered)).Should().Throw<ArgumentException>();
        series.Count.Should().Be(2);
        series.Snapshot().Select(point => point.Value).Should().Equal(7, 8);
    }

    [Fact]
    public void Should_ReturnTheNewStoredPoints_WhenUpdateSinceIsAsked()
    {
        var series = new DataSeries<long, int>("update", new IndexKeys());
        for (var i = 1; i <= 5; i++)
            series.Add(i);

        var update = series.UpdateSince(3);
        update.Count.Should().Be(5);
        update.SeriesName.Should().Be("update");
        ((KeyValuePair<long, int>[])update.Points).Select(point => point.Value).Should().Equal(4, 5);
        series.UpdateSince(5).Points.Length.Should().Be(0);

        series.Clear();
        series.Add(10);
        series.Add(11);
        ((KeyValuePair<long, int>[])series.UpdateSince(5).Points).Select(point => point.Value).Should().Equal(10, 11);
        FluentActions.Invoking(() => series.UpdateSince(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_TakeTheIdFromTheName_WhenNoIdIsGiven()
    {
        new DataSeries<long, int>("a", new IndexKeys()).Id.Should().Be(HierId.From("a"));
        new DataSeries<long, int>("a", new IndexKeys(), id: HierId.From("g", "a")).Id.Should().Be(HierId.From("g", "a"));
    }
}
