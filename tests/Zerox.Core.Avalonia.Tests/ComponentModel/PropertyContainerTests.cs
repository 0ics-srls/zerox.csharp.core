using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Avalonia.ComponentModel;
using Zerox.Core.ComponentModel;

namespace Zerox.Core.Avalonia.Tests.ComponentModel;

public sealed class PropertyContainerTests
{
    public sealed class Plain : NotifyingObject
    {
        private string _value = "";

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    public sealed class Sample(TimeProvider? time = null) : PropertyContainer(time)
    {
        private string _name = "";
        private Sample? _childProp;
        private Plain? _plainProp;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public Sample? ChildProp
        {
            get => _childProp;
            set => SetComplexProperty(ref _childProp, value);
        }

        public Plain? PlainProp
        {
            get => _plainProp;
            set => SetComplexProperty(ref _plainProp, value);
        }
    }

    private sealed record Seen(string? Name, bool OnUi);

    private static bool OnUi => SynchronizationContext.Current is AvaloniaSynchronizationContext;

    private static List<Seen> Members(PropertyContainer container)
    {
        var seen = new List<Seen>();
        container.MemberChanged += (_, e) => seen.Add(new(e.PropertyName, OnUi));
        return seen;
    }

    private static List<Seen> Properties(INotifyPropertyChanged source)
    {
        var seen = new List<Seen>();
        source.PropertyChanged += (_, e) => seen.Add(new(e.PropertyName, OnUi));
        return seen;
    }

    private static void Tick(FakeTimeProvider clock, TimeSpan step)
    {
        clock.Advance(step);
        Dispatcher.UIThread.RunJobs();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachFirstChild(Sample container)
    {
        var child = new Sample();
        container.ChildProp = child;
        return new WeakReference(child);
    }

    [Fact]
    public Task Should_RaisePropertyChangedOnTheUiThread_WhenAPropertyChangesOnAnotherThread() => HeadlessUi.RunAsync(async () =>
    {
        var container = new Sample();
        var members = Members(container);
        var properties = Properties(container);

        await Task.Run(() => container.Name = "a");
        Dispatcher.UIThread.RunJobs();

        members.Should().Equal(new Seen("Name", false));
        properties.Should().Equal(new Seen("Name", true));
    });

    [Fact]
    public Task Should_RaiseMemberChangedAtOnce_WhenAPropertyChanges() => HeadlessUi.RunAsync(() =>
    {
        var container = new Sample();
        var members = Members(container);
        var properties = Properties(container);

        container.Name = "a";
        members.Should().HaveCount(1);
        properties.Should().BeEmpty();

        Dispatcher.UIThread.RunJobs();
        properties.Should().Equal(new Seen("Name", true));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_RaiseNothing_WhenEventsAreOff() => HeadlessUi.RunAsync(() =>
    {
        var container = new Sample();
        var members = Members(container);
        var properties = Properties(container);

        container.EventsOff();
        container.Name = "a";
        Dispatcher.UIThread.RunJobs();
        members.Should().BeEmpty();
        properties.Should().BeEmpty();

        container.EventsOnAndNotifyAll();
        Dispatcher.UIThread.RunJobs();
        members.Should().Equal(new Seen(null, true));
        properties.Should().Equal(new Seen(null, true));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Test_Throttle() => HeadlessUi.RunAsync(() =>
    {
        var clock = new FakeTimeProvider();
        var interval = TimeSpan.FromMilliseconds(250);
        var cnt = new Sample(clock);
        cnt.ActivateEventThrottling(interval);
        var members = Members(cnt);
        var properties = Properties(cnt);

        for (var i = 0; i < 5000; i++)
            cnt.Name = $"Name{i}";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(properties);

        Tick(clock, interval);
        Assert.Single(properties);
        Assert.Equal("Name4999", cnt.Name);

        for (var i = 0; i < 10; i++)
            cnt.Name = $"More{i}";
        Tick(clock, interval);
        Assert.Equal(2, properties.Count);

        Tick(clock, interval);
        Assert.Equal(2, properties.Count);
        Assert.Equal(5010, members.Count);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Test_Debounce() => HeadlessUi.RunAsync(() =>
    {
        var clock = new FakeTimeProvider();
        var cnt = new Sample(clock);
        cnt.ActivateEventThrottling(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1000));
        var properties = Properties(cnt);

        for (var i = 0; i < 40; i++)
        {
            cnt.Name = $"Name{i}";
            Tick(clock, TimeSpan.FromMilliseconds(50));
        }
        Assert.Empty(properties);

        for (var i = 0; i < 11; i++)
            Tick(clock, TimeSpan.FromMilliseconds(100));
        Assert.Single(properties);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_SendWhatIsWaiting_WhenThrottlingStops() => HeadlessUi.RunAsync(() =>
    {
        var clock = new FakeTimeProvider();
        var container = new Sample(clock);
        container.ActivateEventThrottling(TimeSpan.FromMilliseconds(250));
        var properties = Properties(container);
        container.Name = "a";

        container.StopEventThrottling();
        Dispatcher.UIThread.RunJobs();
        properties.Should().Equal(new Seen("Name", true));
        container.IsEventThrottlingActive.Should().BeFalse();

        container.Name = "b";
        Dispatcher.UIThread.RunJobs();
        properties.Should().HaveCount(2);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_RaiseNothing_WhenDisposed() => HeadlessUi.RunAsync(() =>
    {
        var clock = new FakeTimeProvider();
        var container = new Sample(clock);
        container.ActivateEventThrottling(TimeSpan.FromMilliseconds(250));
        var properties = Properties(container);
        container.Name = "a";

        container.Dispose();
        Tick(clock, TimeSpan.FromMilliseconds(250));
        var members = Members(container);
        container.Name = "b";
        Dispatcher.UIThread.RunJobs();

        properties.Should().BeEmpty();
        members.Should().BeEmpty();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Test_Nested() => HeadlessUi.RunAsync(() =>
    {
        var ccc = new Sample();
        var child = new Sample();
        var counter = 0;
        ccc.MemberChanged += (_, _) => ++counter;

        ccc.ChildProp = child;
        ccc.Name = "Pippo";
        Assert.Equal(2, counter);
        child.Name = "Child of pippo!";
        Assert.Equal(3, counter);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Test_Memory_leaks_Nested() => HeadlessUi.RunAsync(() =>
    {
        var container = new Sample();
        container.MemberChanged += (_, _) => { };
        var weak = AttachFirstChild(container);
        for (var i = 0; i < 1000; ++i)
            container.ChildProp = new Sample { Name = "pippo" };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive, "The old child must be collectible after it is replaced.");
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_StopFollowingTheOldValue_WhenReplaced() => HeadlessUi.RunAsync(() =>
    {
        var container = new Sample();
        var oldChild = new Sample();
        var newChild = new Sample();
        container.ChildProp = oldChild;
        container.ChildProp = newChild;
        var members = Members(container);

        oldChild.Name = "old";
        members.Should().BeEmpty();
        newChild.Name = "new";
        members.Should().Equal(new Seen("ChildProp", true));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_FollowAnyNotifyingValue_WhenItIsNotAContainer() => HeadlessUi.RunAsync(() =>
    {
        var container = new Sample();
        var plain = new Plain();
        container.PlainProp = plain;
        var members = Members(container);

        plain.Value = "x";

        members.Should().Equal(new Seen("PlainProp", true));
        return Task.CompletedTask;
    });

    [Fact]
    public void Should_RejectTheInterval_WhenItIsNotPositive()
    {
        var container = new Sample();

        FluentActions.Invoking(() => container.ActivateEventThrottling(TimeSpan.Zero)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => container.ActivateEventThrottling(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1))).Should().Throw<ArgumentOutOfRangeException>();
    }
}
