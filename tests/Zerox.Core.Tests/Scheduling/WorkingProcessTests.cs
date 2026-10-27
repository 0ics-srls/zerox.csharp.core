using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Scheduling;

namespace Zerox.Core.Tests.Scheduling;

public sealed class WorkingProcessTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly Channel<long> _cycles = Channel.CreateUnbounded<long>();
    private long _count;

    // Each cycle writes its number; the test reads them in order.
    private WorkingProcess Counting(TimeSpan period, Func<CancellationToken, Task>? body = null)
        => new("Counting", async ct =>
        {
            if (body is not null)
                await body(ct);
            _cycles.Writer.TryWrite(Interlocked.Increment(ref _count));
        }, period, _clock);

    private ValueTask<long> NextCycleAsync() => _cycles.Reader.ReadAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_RunTheFirstCycleAtOnce_WhenStarted()
    {
        await using var process = Counting(TimeSpan.FromMilliseconds(100));

        process.Start();

        (await NextCycleAsync()).Should().Be(1);
        process.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Should_RunACyclePerPeriod_WhenTheClockAdvances()
    {
        await using var process = Counting(TimeSpan.FromMilliseconds(100));
        process.Start();
        (await NextCycleAsync()).Should().Be(1);

        _clock.Advance(TimeSpan.FromMilliseconds(100));
        (await NextCycleAsync()).Should().Be(2);
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        (await NextCycleAsync()).Should().Be(3);

        await process.StopAsync();
        process.CyclesDone.Should().Be(3);
    }

    [Fact]
    public async Task Should_StopCycling_WhenStopped()
    {
        await using var process = Counting(TimeSpan.FromMilliseconds(100));
        process.Start();
        (await NextCycleAsync()).Should().Be(1);

        await process.StopAsync();
        process.IsActive.Should().BeFalse();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _cycles.Reader.TryRead(out _).Should().BeFalse();

        process.Start();
        (await NextCycleAsync()).Should().Be(2);
        process.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Should_MeasureTheCycles_WhenTheyRun()
    {
        await using var process = Counting(TimeSpan.FromSeconds(1), Work.Of(() => _clock.Advance(TimeSpan.FromMilliseconds(30))));
        process.Start();
        (await NextCycleAsync()).Should().Be(1);

        _clock.Advance(TimeSpan.FromMilliseconds(970));
        (await NextCycleAsync()).Should().Be(2);
        await process.StopAsync();

        process.CyclesDone.Should().Be(2);
        process.LastCycle.Should().Be(TimeSpan.FromMilliseconds(30));
        process.MeanCycle.Should().Be(TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public async Task Should_RunTheScheduler_WhenItIsTheCycle()
    {
        var scheduler = new JobScheduler("Scheduler", _clock);
        int a = 0, b = 0, p = 0;
        scheduler.CreateMainJob("A", Work.Of(() => a++));
        scheduler.CreateMainJob("B", Work.Of(() => b++));
        scheduler.CreatePriorityJob("P", Work.Of(() => p++));
        await using var process = Counting(TimeSpan.FromMilliseconds(10), scheduler.TickAsync);
        process.Start();
        (await NextCycleAsync()).Should().Be(1);

        for (var cycle = 2; cycle <= 6; cycle++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(10));
            (await NextCycleAsync()).Should().Be(cycle);
        }
        await process.StopAsync();

        // main ring: SlowQueue, A, B
        a.Should().Be(2);
        b.Should().Be(2);
        p.Should().Be(6);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperation_WhenStartedTwice()
    {
        await using var process = Counting(TimeSpan.FromMilliseconds(100));
        process.Start();

        FluentActions.Invoking(process.Start).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_RejectThePeriod_WhenItIsNotPositive()
    {
        FluentActions.Invoking(() => new WorkingProcess("Zero", _ => Task.CompletedTask, TimeSpan.Zero, _clock))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
