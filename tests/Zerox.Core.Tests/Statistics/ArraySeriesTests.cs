using System.Globalization;
using Zerox.Core.Collections;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class ArraySeriesTests
{
    [Fact]
    public void ArraySeries()
    {
        const int size = 10;
        var keys = new IndexKeys();
        var sr = new ArraySeries<long, int>("SR1", size, keys);
        var sr16 = new DataSeries<long, int>("SR2", new IndexKeys());
        for (var i = 0; i < size; ++i)
        {
            var d = new int[size];
            for (var j = 0; j < size; ++j)
                d[j] = i;
            sr.Add(d);
            sr16.Add(i);
        }

        var sum = new double[size];
        for (var j = 0; j < size; ++j)
            sum[j] = sr16.Sum;
        var s1 = string.Join(",", sum.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        var s2 = string.Join(",", sr.ViewSum(0, 10).Select(v => v.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(s1, s2);
        Assert.Equal(45, sr16.Sum);
    }

    [Fact]
    public void Should_RejectTheRow_WhenItsLengthIsNotTheWidth()
    {
        var series = new ArraySeries<long, int>("width", 3, new IndexKeys());

        FluentActions.Invoking(() => series.Add([1, 2])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => series.Add([1, 2, 3, 4])).Should().Throw<ArgumentException>();
        series.Count.Should().Be(0);
    }

    [Fact]
    public void Should_CopyTheRow_WhenTheCallerReusesItsArray()
    {
        var series = new ArraySeries<long, int>("copy", 2, new IndexKeys());
        var row = new[] { 1, 2 };

        series.Add(row);
        row[0] = 7;
        row[1] = 8;
        series.Add(row);

        series.Get(0).Value.Should().Equal(1, 2);
        series.Get(1).Value.Should().Equal(7, 8);
    }

    [Fact]
    public void Should_SetTheRateToZero_WhenAChannelStopsChanging()
    {
        var series = new ArraySeries<long, int>("rate", 1, new IndexKeys());

        series.Add([0], 0);
        series.Add([5], 1);
        series.Rate[0].Should().Be(5);
        series.Add([5], 2);

        series.Rate[0].Should().Be(0);
    }

    [Fact]
    public void Should_ComputePerChannelStatistics_WhenRowsArrive()
    {
        var series = new ArraySeries<long, int>("stats", 2, new IndexKeys());

        series.Add([1, 10]);
        series.Add([3, 20]);
        series.Add([2, 30]);

        series.Min.Should().Equal(1, 10);
        series.Max.Should().Equal(3, 30);
        series.Sum.Should().Equal(6.0, 60.0);
        series.Mean.Should().Equal(2.0, 20.0);
        series.Last.Should().Equal(2, 30);
        series.Count.Should().Be(3);
    }

    [Fact]
    public void Should_JoinTheParts_WhenAddParts()
    {
        var series = new ArraySeries<long, int>("parts", 4, new IndexKeys());

        series.AddParts([1, 2], [3, 4]);

        series.Last.Should().Equal(1, 2, 3, 4);
        FluentActions.Invoking(() => series.AddParts([1, 2], [3])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_ViewOneChannel_WhenViewChannel()
    {
        var series = new ArraySeries<long, int>("channel", 2, new IndexKeys());
        series.Add([0, 0], 0);
        series.Add([1, 10], 1);
        series.Add([2, 20], 2);

        series.ViewChannel(Interval.Range(1L, 3L), 1).Should().Equal(new KeyValuePair<long, int>(1, 10), new KeyValuePair<long, int>(2, 20));
        FluentActions.Invoking(() => series.ViewChannel(Interval.Range(1L, 3L), 2)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_ReturnTheNewStoredRows_WhenUpdateSinceIsAsked()
    {
        var series = new ArraySeries<long, int>("update", 1, new IndexKeys());
        for (var i = 0; i < 4; i++)
            series.Add([i]);

        var update = series.UpdateSince(1);

        update.Count.Should().Be(4);
        update.Points.Length.Should().Be(3);
    }
}
