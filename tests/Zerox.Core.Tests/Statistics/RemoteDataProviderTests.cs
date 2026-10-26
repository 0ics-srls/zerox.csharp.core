using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Identity;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class RemoteDataProviderTests
{
    private static readonly TimeSpan Period = RemoteDataProvider.DefaultPeriod;

    private readonly FakeTimeProvider _clock = new();
    private readonly SeriesAggregator _aggregator = new();

    private DataSeries<long, int> Series(string name)
    {
        var series = new DataSeries<long, int>(name, new IndexKeys());
        _aggregator.Register(series);
        return series;
    }

    private RemoteDataProvider Provider() => new(_aggregator, _clock, Period);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static int[] Values(SeriesUpdate update) => [.. ((KeyValuePair<long, int>[])update.Points).Select(point => point.Value)];

    [Fact]
    public async Task Should_SendTheNewPoints_WhenThePeriodElapses()
    {
        var a = Series("a");
        await using var provider = Provider();
        provider.Start(["a"]);

        a.Add(1);
        a.Add(2);
        a.Add(3);
        _clock.Advance(Period);
        var update = await provider.Updates.ReadAsync(Token);

        update.SeriesName.Should().Be("a");
        Values(update).Should().Equal(1, 2, 3);
        update.Count.Should().Be(3);
    }

    [Fact]
    public async Task Should_SendOnlyWhatIsNew_WhenTheNextPeriodElapses()
    {
        var a = Series("a");
        await using var provider = Provider();
        provider.Start(["a"]);
        a.Add(1);
        a.Add(2);
        a.Add(3);
        _clock.Advance(Period);
        await provider.Updates.ReadAsync(Token);

        a.Add(4);
        a.Add(5);
        _clock.Advance(Period);
        var update = await provider.Updates.ReadAsync(Token);

        Values(update).Should().Equal(4, 5);
        update.Count.Should().Be(5);
    }

    [Fact]
    public async Task Should_CrawlEverySeriesOfAGroup_WhenTheNameIsAGroup()
    {
        Series("a").Add(1);
        Series("b").Add(2);
        _aggregator.Register(SeriesGroup.Of("g", HierId.From("a"), HierId.From("b")));
        await using var provider = Provider();
        provider.Start(["g"]);

        _clock.Advance(Period);
        var first = await provider.Updates.ReadAsync(Token);
        var second = await provider.Updates.ReadAsync(Token);
        await provider.StopAsync();

        new[] { first.SeriesName, second.SeriesName }.Should().BeEquivalentTo("a", "b");
        provider.Updates.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Should_SendASeriesOnce_WhenTwoNamesContainIt()
    {
        Series("a").Add(1);
        _aggregator.Register(SeriesGroup.Of("g", HierId.From("a")));
        await using var provider = Provider();
        provider.Start(["a", "g"]);

        _clock.Advance(Period);
        var update = await provider.Updates.ReadAsync(Token);
        await provider.StopAsync();

        update.SeriesName.Should().Be("a");
        provider.Updates.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Should_RejectTheName_WhenNoSeriesOrGroupHasIt()
    {
        await using var provider = Provider();

        FluentActions.Invoking(() => provider.Start(["none"])).Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Should_ThrowInvalidOperation_WhenStartedTwice()
    {
        Series("a");
        await using var provider = Provider();
        provider.Start(["a"]);

        FluentActions.Invoking(() => provider.Start(["a"])).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_StopSending_WhenStopped()
    {
        var a = Series("a");
        await using var provider = Provider();
        provider.Start(["a"]);

        await provider.StopAsync();
        a.Add(1);
        _clock.Advance(TimeSpan.FromSeconds(1));
        provider.Updates.TryRead(out _).Should().BeFalse();

        provider.Start(["a"]);
        _clock.Advance(Period);
        Values(await provider.Updates.ReadAsync(Token)).Should().Equal(1);
    }

    [Fact]
    public async Task Should_ListTheAvailableSeries_WhenAsked()
    {
        Series("b");
        Series("a");
        _aggregator.Register(SeriesGroup.Of("g", HierId.From("a")));
        await using var provider = Provider();

        provider.AvailableSeries().Should().Equal("a", "b", "g");
    }

    [Fact]
    public void Should_RejectThePeriod_WhenItIsNotPositive()
        => FluentActions.Invoking(() => new RemoteDataProvider(_aggregator, _clock, TimeSpan.Zero)).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public async Task Should_CompleteTheUpdates_WhenDisposed()
    {
        Series("a");
        var provider = Provider();
        provider.Start(["a"]);

        await provider.DisposeAsync();

        provider.Updates.Completion.IsCompleted.Should().BeTrue();
    }
}
