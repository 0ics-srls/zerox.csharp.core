using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Identity;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class SeriesAggregatorTests
{
    private static readonly TimeKeys Keys = new(new FakeTimeProvider());

    private static DataSeries<DateTime, int> TimeSeries(string name) => new(name, Keys);

    private static ArraySeries<long, int> Rows(string name, HierId? id = null) => new(name, 5, new IndexKeys(), id: id);

    [Fact]
    public void MakeSeries()
    {
        var da = new SeriesAggregator();

        da.Register(TimeSeries("SR1"));
        da.Register(TimeSeries("SR2"));
        Assert.Throws<ArgumentException>(() => da.Register(TimeSeries("SR2")));
        Assert.Throws<ArgumentException>(() => da.Register(TimeSeries("SR1")));

        Assert.NotNull(da.Get<DataSeries<DateTime, int>>("SR1"));
        Assert.NotNull(da.Get<DataSeries<DateTime, int>>("SR2"));
        Assert.Throws<ArgumentException>(() => da.Get<DataSeries<long, int>>("SR2"));
        Assert.Throws<ArgumentException>(() => da.Get<DataSeries<long, int>>("SR1"));
    }

    [Fact]
    public void MakeSeriesWithGroups()
    {
        var da = new SeriesAggregator();
        foreach (var name in new[] { "SR1", "SR2", "SR3", "SR4", "SR5" })
            da.Register(TimeSeries(name));

        da.Register(Rows("ASR1", HierId.From("G1", "s1", "n1")));
        da.Register(Rows("ASR2", HierId.From("G1", "s1", "n2")));
        da.Register(Rows("ASR3", HierId.From("G2", "s1", "n3")));
        da.Register(Rows("ASR4", HierId.From("G1", "s2", "n4")));
        da.Register(Rows("ASR5"));

        da.Register(SeriesGroup.Of("G1.s1", HierId.Parse("G1/s1/n1"), HierId.Parse("G1/s1/n2")));
        da.Register(SeriesGroup.Of("G2.s1", HierId.Parse("G2/s1/n3")));
        da.Register(SeriesGroup.Of("G1.s2", HierId.Parse("G1/s2/n4")));
        da.Register(SeriesGroup.WithPrefix("Pfx-G1.s1", HierId.Parse("G1/s1")));
        da.Register(SeriesGroup.WithPrefix("Pfx-G2.s1", HierId.Parse("G2/s1")));
        da.Register(SeriesGroup.WithPrefix("Pfx-G1.s2", HierId.Parse("G1/s2")));
        da.Register(SeriesGroup.WithPrefix("Pfx-G1", HierId.Parse("G1")));

        Assert.Equal("SR1, SR2, SR3, SR4, SR5", string.Join(", ", da.AvailableNames<DataSeries<DateTime, int>>()));
        Assert.Equal("ASR1, ASR2, ASR3, ASR4, ASR5, G1.s1, G1.s2, G2.s1, Pfx-G1, Pfx-G1.s1, Pfx-G1.s2, Pfx-G2.s1", string.Join(", ", da.AvailableNames<ArraySeries<long, int>>()));
        Assert.Single(da.GetSeriesOrGroup<ArraySeries<long, int>>("ASR1"));
        Assert.Equal(2, da.GetSeriesOrGroup<ArraySeries<long, int>>("G1.s1").Count);
        Assert.Single(da.GetSeriesOrGroup<ArraySeries<long, int>>("G2.s1"));
        Assert.Single(da.GetSeriesOrGroup<ArraySeries<long, int>>("G1.s2"));
        Assert.Equal(2, da.GetSeriesOrGroup<ArraySeries<long, int>>("Pfx-G1.s1").Count);
        Assert.Single(da.GetSeriesOrGroup<ArraySeries<long, int>>("Pfx-G2.s1"));
        Assert.Single(da.GetSeriesOrGroup<ArraySeries<long, int>>("Pfx-G1.s2"));
        Assert.Equal(3, da.GetSeriesOrGroup<ArraySeries<long, int>>("Pfx-G1").Count);
    }

    [Fact]
    public void TestSeries()
    {
        var da = new SeriesAggregator();
        da.Register(TimeSeries("SR1"));
        da.Register(TimeSeries("SR2"));
        var sr16 = da.Get<DataSeries<DateTime, int>>("SR1");
        var sr32 = da.Get<DataSeries<DateTime, int>>("SR2");

        for (var i = 0; i < 100; ++i)
        {
            sr16.Add(i);
            da.Add<int>("SR2", i);
        }

        Assert.Equal(sr16.Mean, sr32.Mean);
        Assert.Equal(100, sr32.Count);
    }

    [Fact]
    public void Should_ThrowArgument_WhenAddingToAMissingSeries()
        => FluentActions.Invoking(() => new SeriesAggregator().Add("none", 1)).Should().Throw<ArgumentException>();

    [Fact]
    public void Should_ThrowArgument_WhenTheValueTypeDoesNotMatch()
    {
        var da = new SeriesAggregator();
        da.Register(new DataSeries<long, int>("ints", new IndexKeys()));
        da.Register(Rows("rows"));

        FluentActions.Invoking(() => da.Add("ints", 1.5)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => da.Add("rows", 1)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_FindEverySeries_WhenTwoShareTheSameId()
    {
        var da = new SeriesAggregator();
        var id = HierId.From("G", "x");
        da.Register(new DataSeries<long, int>("first", new IndexKeys(), id: id));
        da.Register(new DataSeries<long, int>("second", new IndexKeys(), id: id));
        da.Register(SeriesGroup.Of("g", id));

        da.GetSeriesOrGroup<ISeries>("g").Select(series => series.Name).Should().Equal("first", "second");
    }

    [Fact]
    public async Task Should_RegisterOnce_WhenManyThreadsRegisterTheSameName()
    {
        var da = new SeriesAggregator();
        using var start = new Barrier(8);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait(TestContext.Current.CancellationToken);
            return da.TryRegister(new DataSeries<long, int>("same", new IndexKeys()));
        }, TestContext.Current.CancellationToken)));

        results.Count(registered => registered).Should().Be(1);
    }

    [Fact]
    public void Should_FindSeriesByHierId_WhenThePredicateMatches()
    {
        var da = new SeriesAggregator();
        da.Register(new DataSeries<long, int>("c", new IndexKeys(), id: HierId.From("a", "c")));
        da.Register(new DataSeries<long, int>("b", new IndexKeys(), id: HierId.From("a", "b")));
        da.Register(new DataSeries<long, int>("d", new IndexKeys(), id: HierId.From("d")));

        da.GetByHierId<ISeries>(id => id.StartsWith(HierId.From("a"))).Select(series => series.Name).Should().Equal("b", "c");
    }

    [Fact]
    public void Should_ForgetTheSeries_WhenRemoved()
    {
        var da = new SeriesAggregator();
        da.Register(TimeSeries("SR1"));

        da.Remove("SR1").Should().BeTrue();
        FluentActions.Invoking(() => da.Get<DataSeries<DateTime, int>>("SR1")).Should().Throw<ArgumentException>();
        da.Remove("SR1").Should().BeFalse();
    }

    [Fact]
    public void Should_ReturnNothing_WhenTheNameIsUnknown()
        => new SeriesAggregator().GetSeriesOrGroup<ISeries>("none").Should().BeEmpty();
}
