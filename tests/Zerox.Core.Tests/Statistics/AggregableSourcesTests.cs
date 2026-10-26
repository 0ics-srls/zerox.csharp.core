using System.ComponentModel;
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Identity;
using Zerox.Core.Statistics;

namespace Zerox.Core.Tests.Statistics;

public sealed class AggregableSourcesTests
{
    public abstract class Notifier(string name) : IAggregableSource
    {
        public string Name { get; } = name;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string? property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    public sealed class SystemMonitor(string name = "Monitor") : Notifier(name)
    {
        private double _usedRam;

        [Aggregable(Group = "SysInfo", Name = "Used ram", Color = "#00FF00")]
        public double UsedRam
        {
            get => _usedRam;
            set
            {
                _usedRam = value;
                Raise(nameof(UsedRam));
            }
        }

        [Aggregable]
        public int Threads { get; set; }

        public double Other { get; set; }
    }

    // A getter that refreshes its value and raises its own change (as the old SystemResourcesMonitor did).
    public sealed class Refreshing() : Notifier("Refreshing")
    {
        private double _load;

        [Aggregable]
        public double Load
        {
            get
            {
                _load += 1;
                Raise(nameof(Load));
                return _load;
            }
        }
    }

    public sealed class Labeled() : Notifier("Labeled")
    {
        [Aggregable]
        public string Label { get; set; } = "";
    }

    public sealed class Broken() : Notifier("Broken")
    {
        [Aggregable]
        public double Value => Fail(Name);

        private static double Fail(string name) => throw new InvalidOperationException($"{name}: broken sensor");
    }

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly SeriesAggregator _aggregator = new();

    private AggregableSources Sources() => new(_aggregator, _clock);

    [Fact]
    public void Should_CreateOneSeriesPerMarkedProperty_WhenASourceIsRegistered()
    {
        var series = Sources().Register(new SystemMonitor());

        series.Select(s => s.Name).Should().Equal("Monitor.Used ram", "Monitor.Threads");
        series[0].Id.Should().Be(HierId.From("SysInfo", "Monitor", "Used ram"));
        series[1].Id.Should().Be(HierId.From("Monitor", "Threads"));
        _aggregator.Series.Select(s => s.Name).Should().Equal("Monitor.Threads", "Monitor.Used ram");
    }

    [Fact]
    public void Should_RegisterThePrefixGroup_WhenAPropertyNamesAGroup()
    {
        var sources = Sources();
        sources.Register(new SystemMonitor());
        _aggregator.GetSeriesOrGroup<ISeries>("SysInfo").Select(s => s.Name).Should().Equal("Monitor.Used ram");

        sources.Register(new SystemMonitor("Monitor2"));

        _aggregator.GetSeriesOrGroup<ISeries>("SysInfo").Select(s => s.Name).Should().Equal("Monitor.Used ram", "Monitor2.Used ram");
    }

    [Fact]
    public void Should_AddTheValue_WhenTheSourceRaisesPropertyChanged()
    {
        var monitor = new SystemMonitor();
        var series = Sources().Register(monitor);
        _clock.Advance(TimeSpan.FromSeconds(3));

        monitor.UsedRam = 42;

        series[0].Count.Should().Be(1);
        series[0].Last.Should().Be(42);
        series[0].LastPair.Key.Should().Be(_clock.GetUtcNow().UtcDateTime);
        series[1].Count.Should().Be(0);
    }

    [Fact]
    public void Should_SampleEveryProperty_WhenAnEmptyNameIsRaised()
    {
        var monitor = new SystemMonitor { Threads = 7 };
        var series = Sources().Register(monitor);

        monitor.Raise(null);

        series.Select(s => s.Count).Should().Equal(1, 1);
        series[1].Last.Should().Be(7);
    }

    [Fact]
    public void Should_SampleEveryProperty_WhenSampleIsCalled()
    {
        var sources = Sources();
        var first = sources.Register(new SystemMonitor { Threads = 3 });
        var second = sources.Register(new SystemMonitor("Monitor2") { Threads = 5 });

        sources.Sample();

        first.Concat(second).Select(s => s.Count).Should().Equal(1, 1, 1, 1);
        first[1].Last.Should().Be(3);
        second[1].Last.Should().Be(5);
    }

    [Fact]
    public void Should_SampleOnce_WhenTheGetterRaisesPropertyChangedDuringSample()
    {
        var sources = Sources();
        var series = sources.Register(new Refreshing());

        sources.Sample();

        series[0].Count.Should().Be(1);
        series[0].Last.Should().Be(1);
    }

    [Fact]
    public void Should_RejectTheSource_WhenAMarkedPropertyIsNotNumeric()
    {
        FluentActions.Invoking(() => Sources().Register(new Labeled())).Should().Throw<ArgumentException>();
        _aggregator.Series.Should().BeEmpty();
    }

    [Fact]
    public void Should_RejectTheSource_WhenItIsRegisteredTwice()
    {
        var sources = Sources();
        var monitor = new SystemMonitor();
        var series = sources.Register(monitor);

        FluentActions.Invoking(() => sources.Register(monitor)).Should().Throw<ArgumentException>();
        monitor.UsedRam = 1;

        series[0].Count.Should().Be(1);
        _aggregator.Series.Should().HaveCount(2);
    }

    [Fact]
    public void Should_RollBack_WhenASeriesNameIsTaken()
    {
        _aggregator.Register(new DataSeries<long, int>("Monitor.Threads", new IndexKeys()));

        FluentActions.Invoking(() => Sources().Register(new SystemMonitor())).Should().Throw<ArgumentException>();

        _aggregator.Series.Select(s => s.Name).Should().Equal("Monitor.Threads");
    }

    [Fact]
    public void Should_StopListening_WhenTheSourceIsUnregistered()
    {
        var sources = Sources();
        var monitor = new SystemMonitor();
        var series = sources.Register(monitor);

        sources.Unregister(monitor).Should().BeTrue();
        monitor.UsedRam = 9;

        _aggregator.Series.Should().BeEmpty();
        series[0].Count.Should().Be(0);
        sources.Unregister(monitor).Should().BeFalse();
    }

    [Fact]
    public void Should_LetTheGetterExceptionReachTheCaller_WhenSampling()
    {
        var sources = Sources();
        sources.Register(new Broken());

        FluentActions.Invoking(sources.Sample).Should().Throw<InvalidOperationException>().WithMessage("Broken: broken sensor");
    }
}
