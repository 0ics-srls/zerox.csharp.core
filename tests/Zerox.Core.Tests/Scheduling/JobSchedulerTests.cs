using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Scheduling;

namespace Zerox.Core.Tests.Scheduling;

public sealed class JobSchedulerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Worker
    {
        public int JM_1_cnt; // no suspend
        public int JM_2_cnt; // suspend-able
        public int JS_1_cnt; // no suspend
        public int JS_2_cnt; // suspend-able
        public int JE_1_cnt; // no suspend
        public int JE_2_cnt; // suspend-able
        public int JC_1_cnt; // no suspend
        public int JC_2_cnt; // suspend-able

        public void Reset()
        {
            JM_1_cnt = JM_2_cnt = JS_1_cnt = JS_2_cnt = 0;
            JE_1_cnt = JE_2_cnt = JC_1_cnt = JC_2_cnt = 0;
        }

        public Task Add_M_1(CancellationToken _)
        {
            JM_1_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_M_2(CancellationToken _)
        {
            JM_2_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_S_1(CancellationToken _)
        {
            JS_1_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_S_2(CancellationToken _)
        {
            JS_2_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_E_1(CancellationToken _)
        {
            JE_1_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_E_2(CancellationToken _)
        {
            JE_2_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_C_1(CancellationToken _)
        {
            JC_1_cnt++;
            return Task.CompletedTask;
        }

        public Task Add_C_2(CancellationToken _)
        {
            JC_2_cnt++;
            return Task.CompletedTask;
        }
    }

    private static JobScheduler WithOldJobs(Worker w, TimeProvider? time = null)
    {
        var sch = new JobScheduler("Scheduler", time ?? new FakeTimeProvider());
        sch.CreateMainJob("JM_1", w.Add_M_1, "Main");
        sch.CreateMainJob("JM_2", w.Add_M_2, "MainSuspend");
        sch.CreateSlowJob("JS_1", w.Add_S_1, "Slow");
        sch.CreateSlowJob("JS_2", w.Add_S_2, "SlowSuspend");
        sch.CreatePriorityJob("JE_1", w.Add_E_1, "Priority");
        sch.CreatePriorityJob("JE_2", w.Add_E_2, "PrioritySuspend");
        return sch;
    }

    private static async Task Ticks(JobScheduler sch, int count)
    {
        for (var i = 0; i < count; i++)
            await sch.TickAsync(Ct);
    }

    private static void AssertOldRunningCounts(Worker w)
    {
        // main ring: SlowQueue, JM_1, JM_2 → a lap of 3 ticks runs one slow job
        Assert.Equal(200, w.JM_1_cnt);
        Assert.Equal(200, w.JM_2_cnt);
        Assert.Equal(100, w.JS_1_cnt);
        Assert.Equal(100, w.JS_2_cnt);
        Assert.Equal(300, w.JE_1_cnt);
        Assert.Equal(300, w.JE_2_cnt);
    }

    [Fact]
    public async Task Test_Create_Scheduler()
    {
        var w = new Worker();
        var sch = new JobScheduler("Scheduler", new FakeTimeProvider());
        sch.CreateMainJob("JM_1", w.Add_M_1);
        sch.CreateSlowJob("JS_1", w.Add_S_1);
        sch.CreatePriorityJob("JE_1", w.Add_E_1);

        await sch.TickAsync(Ct);

        // the first main element is the slow queue
        Assert.Equal(0, w.JM_1_cnt);
        Assert.Equal(1, w.JS_1_cnt);
        Assert.Equal(1, w.JE_1_cnt);
    }

    [Fact]
    public async Task Test_Main_And_Slow_Running()
    {
        var w = new Worker();
        var sch = WithOldJobs(w);

        await Ticks(sch, 600);

        AssertOldRunningCounts(w);
    }

    [Fact]
    public async Task Test_Main_And_Slow_And_custom_Running()
    {
        var w = new Worker();
        var sch = WithOldJobs(w);
        sch.CreateJobQueue("CustomQueue");
        sch.CreateCustomJob("JC_1", "CustomQueue", w.Add_C_1, "Custom");
        sch.CreateCustomJob("JC_2", "CustomQueue", w.Add_C_2, "CustomSuspend");

        await Ticks(sch, 800);

        // main ring: SlowQueue, JM_1, JM_2, CustomQueue → a lap of 4 ticks
        Assert.Equal(200, w.JM_1_cnt);
        Assert.Equal(200, w.JM_2_cnt);
        Assert.Equal(100, w.JS_1_cnt);
        Assert.Equal(100, w.JS_2_cnt);
        Assert.Equal(100, w.JC_1_cnt);
        Assert.Equal(100, w.JC_2_cnt);
        Assert.Equal(400, w.JE_1_cnt);
        Assert.Equal(400, w.JE_2_cnt);
    }

    [Fact]
    public async Task Test_Main_And_Slow_suspended()
    {
        var w = new Worker();
        var sch = WithOldJobs(w);
        sch.SuspendGroup("MainSuspend");
        sch.SuspendGroup("PrioritySuspend");

        await Ticks(sch, 600);

        // main ring: SlowQueue, JM_1; priority ring: JE_1
        Assert.Equal(300, w.JM_1_cnt);
        Assert.Equal(0, w.JM_2_cnt);
        Assert.Equal(150, w.JS_1_cnt);
        Assert.Equal(150, w.JS_2_cnt);
        Assert.Equal(600, w.JE_1_cnt);
        Assert.Equal(0, w.JE_2_cnt);

        sch.ResumeGroup("MainSuspend");
        sch.ResumeGroup("PrioritySuspend");
        sch.Reset(); // reset queue pointers
        w.Reset();

        await Ticks(sch, 600);

        AssertOldRunningCounts(w);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Test_Multithread_Suspend_And_Resume()
    {
        var w = new Worker();
        var sch = WithOldJobs(w);
        var ct = TestContext.Current.CancellationToken;

        var mainToggler = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                sch.SuspendGroup("MainSuspend");
                sch.ResumeGroup("MainSuspend");
            }
        }, ct);
        var slowToggler = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                sch.SuspendGroup("SlowSuspend");
                sch.ResumeGroup("SlowSuspend");
            }
        }, ct);
        var adder = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
                sch.CreatePriorityOneTimeJob("SomeError", w.Add_C_1);
        }, ct);
        var ticker = Task.Run(async () =>
        {
            // the counters are written only by this flow
            var ticks = 0;
            while ((ticks < 200_000 || w.JC_1_cnt < 50) && ticks < 10_000_000)
            {
                await sch.TickAsync(Ct);
                ticks++;
            }
        }, ct);

        await Task.WhenAll(mainToggler, slowToggler, adder, ticker);

        Assert.False(sch.Errors.TryRead(out _));
        Assert.Equal(50, w.JC_1_cnt);
        Assert.True(w.JM_1_cnt >= w.JM_2_cnt, $"{w.JM_1_cnt} >= {w.JM_2_cnt}");
        Assert.True(w.JS_1_cnt >= w.JS_2_cnt, $"{w.JS_1_cnt} >= {w.JS_2_cnt}");
    }

    [Fact]
    public async Task Should_KeepCustomJobsRunning_WhenThePriorityGroupIsSuspended()
    {
        var w = new Worker();
        var sch = new JobScheduler("Scheduler", new FakeTimeProvider());
        sch.CreateJobQueue("CustomQueue");
        sch.CreateCustomJob("JC_1", "CustomQueue", w.Add_C_1);
        sch.CreatePriorityJob("JE_1", w.Add_E_1);

        sch.SuspendGroup(JobScheduler.PriorityGroup);
        await Ticks(sch, 4); // main ring: SlowQueue, CustomQueue

        w.JC_1_cnt.Should().Be(2);
        w.JE_1_cnt.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReportTheFailureAndRunThePriority_WhenAMainJobFails()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
        var sch = new JobScheduler("Scheduler", clock);
        var priorityRuns = 0;
        sch.CreateMainJob("F", Work.Of(() => throw new InvalidOperationException("broken")));
        sch.CreatePriorityJob("P", Work.Of(() => priorityRuns++));

        await FluentActions.Awaiting(() => Ticks(sch, 2)).Should().NotThrowAsync();

        priorityRuns.Should().Be(2);
        sch.Errors.TryRead(out var error).Should().BeTrue();
        error!.JobName.Should().Be("F");
        error.Exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("broken");
        error.At.Should().Be(clock.GetUtcNow());
        sch.Errors.TryRead(out _).Should().BeFalse();

        await Ticks(sch, 2);

        sch.Errors.TryRead(out var second).Should().BeTrue();
        second!.JobName.Should().Be("F");
    }

    [Fact]
    public async Task Should_RunADelayedJobOnce_WhenItsTimeComes()
    {
        var clock = new FakeTimeProvider();
        var sch = new JobScheduler("Scheduler", clock);
        var runs = 0;
        sch.CreatePriorityOneTimeDelayedJob("D", Work.Of(() => runs++), TimeSpan.FromSeconds(5));

        await Ticks(sch, 3);
        runs.Should().Be(0);

        clock.Advance(TimeSpan.FromSeconds(5));
        await sch.TickAsync(Ct);
        runs.Should().Be(1);

        await Ticks(sch, 3);
        runs.Should().Be(1);
    }

    [Fact]
    public void Should_RejectAName_WhenItIsTakenOrUnknown()
    {
        var sch = new JobScheduler("Scheduler", new FakeTimeProvider());
        sch.CreateMainJob("A", Work.Of(() => { }));

        FluentActions.Invoking(() => sch.CreateMainJob("A", Work.Of(() => { }))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => sch.SuspendJob("Nope")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => sch.ResumeGroup("Nope")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => sch.CreateCustomJob("C", "NoQueue", Work.Of(() => { }))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Should_DoNothing_WhenTheSchedulerIsSuspended()
    {
        var w = new Worker();
        var sch = WithOldJobs(w);

        sch.Suspend();
        await Ticks(sch, 6);

        sch.IsSuspended.Should().BeTrue();
        (w.JM_1_cnt + w.JM_2_cnt + w.JS_1_cnt + w.JS_2_cnt + w.JE_1_cnt + w.JE_2_cnt).Should().Be(0);

        sch.Resume();
        await Ticks(sch, 6);

        sch.IsSuspended.Should().BeFalse();
        w.JM_1_cnt.Should().Be(2);
        w.JE_1_cnt.Should().Be(3);
    }
}
