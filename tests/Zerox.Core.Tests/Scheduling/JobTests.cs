using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Scheduling;

namespace Zerox.Core.Tests.Scheduling;

public sealed class JobTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Test_Job()
    {
        var cycDone = false;
        var cyclic = Job.Cyclic("J1", Work.Of(() => cycDone = true));
        cyclic.Resume();
        Assert.Equal(JobKind.Cyclic, cyclic.Kind);
        Assert.False(cycDone);
        await cyclic.ExecuteAsync(Ct);
        Assert.True(cycDone);

        var runDone = false;
        var once = Job.Once("J2", Work.Of(() => runDone = true));
        Assert.Equal(JobKind.RunOnce, once.Kind);
        Assert.False(runDone);
        once.Resume();
        await once.ExecuteAsync(Ct);
        Assert.True(runDone);
    }

    [Fact]
    public async Task Should_StartSuspended_WhenCreated()
    {
        var ran = false;
        var job = Job.Cyclic("J", Work.Of(() => ran = true));

        job.State.Should().Be(JobState.Suspended);
        await FluentActions.Awaiting(() => job.ExecuteAsync(Ct)).Should().ThrowAsync<InvalidOperationException>();
        ran.Should().BeFalse();
    }

    [Fact]
    public async Task Should_StaySuspended_WhenSuspendedWhileRunning()
    {
        Job? job = null;
        job = Job.Cyclic("J", Work.Of(() => job!.Suspend()));
        job.Resume();

        await job.ExecuteAsync(Ct);

        job.State.Should().Be(JobState.Suspended);
    }

    [Fact]
    public async Task Should_BeReadyAgain_WhenResumedWhileRunning()
    {
        Job? job = null;
        JobState? during = null;
        job = Job.Cyclic("J", Work.Of(() =>
        {
            job!.Resume();
            during = job.State;
        }));
        job.Resume();

        await job.ExecuteAsync(Ct);

        during.Should().Be(JobState.Running);
        job.State.Should().Be(JobState.Ready);
    }

    [Fact]
    public async Task Should_WaitForItsTime_WhenDelayed()
    {
        var clock = new FakeTimeProvider();
        var runs = 0;
        var job = new DelayedJob("D", Work.Of(() => runs++), clock, clock.GetUtcNow().AddSeconds(10));
        job.Resume();

        (await job.ExecuteAsync(Ct)).Should().BeFalse();
        runs.Should().Be(0);

        clock.Advance(TimeSpan.FromSeconds(10));
        (await job.ExecuteAsync(Ct)).Should().BeTrue();
        runs.Should().Be(1);
        job.Kind.Should().Be(JobKind.RunOnce);
    }
}
