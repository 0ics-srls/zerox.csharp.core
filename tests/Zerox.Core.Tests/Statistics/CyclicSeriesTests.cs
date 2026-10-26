using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class CyclicSeriesTests
{
    [Fact]
    public void CyclicSeries()
    {
        var sr1 = new CyclicSeries<int>("MySeries1", 50);
        var sr2 = new CyclicSeries<int>("MySeries2", 50);
        for (var i = 0; i < 200; ++i)
            sr1.Add(i);
        for (var i = 150; i < 200; ++i)
            sr2.Add(i);

        Assert.Equal((int)(sr1.Mean * 100), (int)(sr2.Mean * 100));
        Assert.Equal(sr1.MaxMean, sr2.MaxMean);
        Assert.NotEqual(sr1.MinMean, sr2.MinMean);
        Assert.Equal(sr1.Values.Min(), sr2.Values.Min());
    }

    [Fact]
    public void Should_ForgetTheOldestValue_WhenTheWindowIsFull()
    {
        var series = new CyclicSeries<int>("window", 3);
        foreach (var value in new[] { 1, 2, 3, 10 })
            series.Add(value);

        series.Values.Should().Equal(2, 3, 10);
        series.Sum.Should().Be(15);
        series.Mean.Should().Be(5);
        series.WindowCount.Should().Be(3);
        series.Count.Should().Be(4);
    }

    [Fact]
    public void Should_ReportTheExtremesOfTheWindow_WhenMinAndMaxAreAsked()
    {
        var series = new CyclicSeries<int>("extremes", 3);
        foreach (var value in new[] { 9, 1, 5, 6 })
            series.Add(value);

        series.Min.Should().Be(1);
        series.Max.Should().Be(6);
        series.Add(7);
        series.Min.Should().Be(5);
        series.Max.Should().Be(7);
    }

    [Fact]
    public void Should_TrackTheExtremesOfTheMean_WhenTheMeanMoves()
    {
        var series = new CyclicSeries<int>("means", 2);
        foreach (var value in new[] { 4, 0, 10 })
            series.Add(value);

        series.MinMean.Should().Be(2);
        series.MaxMean.Should().Be(5);
    }

    [Fact]
    public void Should_ReportZero_WhenTheSeriesIsEmpty()
    {
        var series = new CyclicSeries<double>("empty", 3);
        AssertEmpty(series);

        series.Add(4.5);
        series.Clear();
        AssertEmpty(series);

        static void AssertEmpty(CyclicSeries<double> series)
        {
            series.Sum.Should().Be(0);
            series.Mean.Should().Be(0);
            series.Min.Should().Be(0);
            series.Max.Should().Be(0);
            series.MinMean.Should().Be(0);
            series.MaxMean.Should().Be(0);
            series.Last.Should().Be(0);
            series.Count.Should().Be(0);
            series.WindowCount.Should().Be(0);
            series.Values.Should().BeEmpty();
        }
    }

    [Fact]
    public void Should_RejectTheSize_WhenItIsLessThanOne()
        => FluentActions.Invoking(() => new CyclicSeries<int>("zero", 0)).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Should_ReturnTheNewValues_WhenUpdateSinceIsAsked()
    {
        var series = new CyclicSeries<int>("update", 3);
        for (var i = 1; i <= 5; i++)
            series.Add(i);

        ((int[])series.UpdateSince(3).Points).Should().Equal(4, 5);
        ((int[])series.UpdateSince(0).Points).Should().Equal(3, 4, 5);
        series.UpdateSince(0).Count.Should().Be(5);
    }
}
