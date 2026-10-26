using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Scheduling;

namespace Zerox.Core.Tests.Scheduling;

public sealed class JobTests
{
    [Fact]
    public void Test_Job()
    {
        var cycDone = false;
        var cyclic = Job.Cyclic("J1", () => cycDone = true);
        cyclic.Resume();
        Assert.Equal(JobKind.Cyclic, cyclic.Kind);
        Assert.False(cycDone);
        cyclic.Execute();
        Assert.True(cycDone);

        var runDone = false;
        var once = Job.Once("J2", () => runDone = true);
        Assert.Equal(JobKind.RunOnce, once.Kind);
        Assert.False(runDone);
        once.Resume();
        once.Execute();
        Assert.True(runDone);
    }

    [Fact]
    public void Should_StartSuspended_WhenCreated()
    {
        var ran = false;
        var job = Job.Cyclic("J", () => ran = true);

        job.State.Should().Be(JobState.Suspended);
        FluentActions.Invoking(() => job.Execute()).Should().Throw<InvalidOperationException>();
        ran.Should().BeFalse();
    }

    [Fact]
    public void Should_StaySuspended_WhenSuspendedWhileRunning()
    {
        Job? job = null;
        job = Job.Cyclic("J", () => job!.Suspend());
        job.Resume();

        job.Execute();

        job.State.Should().Be(JobState.Suspended);
    }

    [Fact]
    public void Should_BeReadyAgain_WhenResumedWhileRunning()
    {
        Job? job = null;
        JobState? during = null;
        job = Job.Cyclic("J", () =>
        {
            job!.Resume();
            during = job.State;
        });
        job.Resume();

        job.Execute();

        during.Should().Be(JobState.Running);
        job.State.Should().Be(JobState.Ready);
    }

    [Fact]
    public void Should_WaitForItsTime_WhenDelayed()
    {
        var clock = new FakeTimeProvider();
        var runs = 0;
        var job = new DelayedJob("D", () => runs++, clock, clock.GetUtcNow().AddSeconds(10));
        job.Resume();

        job.Execute().Should().BeFalse();
        runs.Should().Be(0);

        clock.Advance(TimeSpan.FromSeconds(10));
        job.Execute().Should().BeTrue();
        runs.Should().Be(1);
        job.Kind.Should().Be(JobKind.RunOnce);
    }
}
