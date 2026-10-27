using Zerox.Core.Scheduling;

namespace Zerox.Core.Tests.Scheduling;

public sealed class JobQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JobQueue NewMain()
    {
        var q = new JobQueue("Main");
        q.Resume();
        return q;
    }

    private static async Task Run(JobQueue q, long times)
    {
        for (var i = 0; i < times; ++i)
            await q.ExecuteAsync(Ct);
    }

    [Fact]
    public async Task Test_JobQueue_run_once()
    {
        var q = NewMain();
        var cnt = 0;
        Job[] jobs = [Job.Once("j1", Work.Of(() => cnt += 1)), Job.Once("j2", Work.Of(() => cnt += 2)), Job.Once("j3", Work.Of(() => cnt += 3)), Job.Once("j4", Work.Of(() => cnt += 4)), Job.Once("j5", Work.Of(() => cnt += 5))];
        foreach (var job in jobs)
            q.Add(job);
        Assert.Equal(0, cnt);
        Assert.All(jobs, job => Assert.True(q.Contains(job)));

        await Run(q, 5);

        Assert.Equal(0, q.Count);
        Assert.Equal(15, cnt);
        Assert.All(jobs, job => Assert.False(q.Contains(job)));
        Assert.All(jobs, job => Assert.Equal(JobState.Suspended, job.State));
    }

    [Fact]
    public async Task Test_JobQueue_run_cyclic()
    {
        var q = NewMain();
        var cnt = 0;
        Job[] jobs = [Job.Cyclic("j1", Work.Of(() => cnt += 1)), Job.Cyclic("j2", Work.Of(() => cnt += 2)), Job.Cyclic("j3", Work.Of(() => cnt += 3)), Job.Cyclic("j4", Work.Of(() => cnt += 4)), Job.Cyclic("j5", Work.Of(() => cnt += 5))];
        foreach (var job in jobs)
            q.Add(job);

        await Run(q, 5);
        Assert.Equal(5, q.Count);
        Assert.Equal(15, cnt);
        Assert.All(jobs, job => Assert.True(q.Contains(job)));

        await Run(q, 5);
        Assert.Equal(5, q.Count);
        Assert.Equal(30, cnt);
        Assert.All(jobs, job => Assert.True(q.Contains(job)));
        Assert.All(jobs, job => Assert.Equal(JobState.Ready, job.State));
    }

    [Fact]
    public async Task Test_JobQueue_run_mixed()
    {
        var q = NewMain();
        var cnt = 0;
        var j1 = Job.Cyclic("j1", Work.Of(() => cnt += 1));
        var j2 = Job.Cyclic("j2", Work.Of(() => cnt += 2));
        var j3 = Job.Cyclic("j3", Work.Of(() => cnt += 3));
        var j4 = Job.Cyclic("j4", Work.Of(() => cnt += 4));
        var j5 = Job.Once("j5", Work.Of(() => cnt += 5));
        foreach (var job in new[] { j1, j2, j3, j4, j5 })
            q.Add(job);

        await Run(q, 5);
        Assert.Equal(4, q.Count);
        Assert.Equal(15, cnt);
        Assert.False(q.Contains(j5));

        await Run(q, 5); // 5 runs execute j1 twice
        Assert.Equal(4, q.Count);
        Assert.Equal(26, cnt);
        Assert.False(q.Contains(j5));
        Assert.All(new[] { j1, j2, j3, j4 }, job => Assert.Equal(JobState.Ready, job.State));
        Assert.Equal(JobState.Suspended, j5.State);
    }

    [Fact]
    public async Task Test_JobQueue_with_child_queue()
    {
        var qMain = NewMain();
        var qChild = new JobQueue("Child");
        var cnt = 0;
        //  main -> j1, j2, j3, ch -> j4, j5
        var j1 = Job.Cyclic("j1", Work.Of(() => cnt += 1));
        var j2 = Job.Cyclic("j2", Work.Of(() => cnt += 2));
        var j3 = Job.Cyclic("j3", Work.Of(() => cnt += 3));
        var j4 = Job.Cyclic("j4", Work.Of(() => cnt += 4));
        var j5 = Job.Cyclic("j5", Work.Of(() => cnt += 5));
        qMain.Add(j1);
        qMain.Add(j2);
        qMain.Add(j3);
        qChild.Add(j4);
        qChild.Add(j5);
        qMain.Add(qChild);

        // +1, +2, +3, (+4), +1, +2, +3, (+5): 2 laps hit every job
        await Run(qMain, 2 * qMain.Count);
        Assert.Equal(21, cnt);

        cnt = 0;
        qMain.Remove(j2);
        qChild.Remove(j4);
        // +1, +3, (+5), +1, +3, (+5)
        await Run(qMain, 2 * qMain.Count);
        Assert.Equal(18, cnt);
        Assert.Equal(JobState.Ready, j1.State);
        Assert.Equal(JobState.Suspended, j2.State);
        Assert.Equal(JobState.Ready, j3.State);
        Assert.Equal(JobState.Ready, qChild.State);
        Assert.Equal(JobState.Suspended, j4.State);
        Assert.Equal(JobState.Ready, j5.State);
    }

    [Fact]
    public async Task Test_JobQueue_with_child_error_queue()
    {
        var qMain = NewMain();
        var qChild = new JobQueue("Child");
        var cnt = 0;
        qMain.Add(Job.Cyclic("j1", Work.Of(() => cnt += 1)));
        qMain.Add(Job.Cyclic("j2", Work.Of(() => cnt += 2)));
        qMain.Add(Job.Cyclic("j3", Work.Of(() => cnt += 3)));
        qChild.Add(Job.Once("j4", Work.Of(() => cnt += 4)));
        qChild.Add(Job.Once("j5", Work.Of(() => cnt += 5)));
        qMain.Add(qChild);

        // +1, +2, +3, (+4), +1, +2, +3, (+5), then +1, +2, +3 with an empty child
        await Run(qMain, 3 * qMain.Count);

        Assert.Equal(27, cnt);
        Assert.Equal(0, qChild.Count);
    }

    [Fact]
    public async Task Test_JobQueue_with_many_jobs()
    {
        var qMain = NewMain();
        var cnt = 0;
        var sum = 0;
        const int len = 10000;
        var jobs = new List<IJob>();
        for (var i = 0; i < len; ++i)
        {
            sum += i;
            var add = i;
            var t = Job.Cyclic($"J{i}", Work.Of(() => cnt += add));
            qMain.Add(t);
            jobs.Add(t);
        }
        Assert.All(jobs, job => Assert.True(qMain.Contains(job)));

        await Run(qMain, len);

        Assert.Equal(sum, cnt);
        Assert.Equal(len, qMain.Count);
    }

    [Fact]
    public async Task Test_JobQueue_with_child_queue_sequence()
    {
        var qMain = NewMain();
        var qChild = new JobQueue("Child");
        var ret = "";
        qMain.Add(Job.Cyclic("j1", Work.Of(() => ret += ",j1")));
        qMain.Add(Job.Cyclic("j2", Work.Of(() => ret += ",j2")));
        qMain.Add(Job.Cyclic("j3", Work.Of(() => ret += ",j3")));
        qChild.Add(Job.Cyclic("j4", Work.Of(() => ret += ",j4")));
        qChild.Add(Job.Cyclic("j5", Work.Of(() => ret += ",j5")));
        qMain.Add(qChild);

        await Run(qMain, 2 * qMain.Count);

        Assert.Equal(",j1,j2,j3,j4,j1,j2,j3,j5", ret);
    }

    [Fact]
    public async Task Should_ReportTheFailureAndGoOn_WhenAnErrorHandlerIsGiven()
    {
        var reported = new List<(string Job, string Message)>();
        var q = new JobQueue("Main", (job, error) => reported.Add((job.Name, error.Message)));
        q.Resume();
        var cyclic = Job.Cyclic("cyclic", Work.Of(() => throw new InvalidOperationException("cyclic failed")));
        var once = Job.Once("once", Work.Of(() => throw new InvalidOperationException("once failed")));
        q.Add(cyclic);
        q.Add(once);

        await FluentActions.Awaiting(() => Run(q, 2)).Should().NotThrowAsync();

        reported.Should().Equal(("cyclic", "cyclic failed"), ("once", "once failed"));
        q.Contains(cyclic).Should().BeTrue();
        q.Contains(once).Should().BeFalse();
    }

    [Fact]
    public async Task Should_ThrowAndRemoveTheOneTimeJob_WhenThereIsNoErrorHandler()
    {
        var q = NewMain();
        var once = Job.Once("once", Work.Of(() => throw new InvalidOperationException("once failed")));
        q.Add(once);

        await FluentActions.Awaiting(() => q.ExecuteAsync(Ct)).Should().ThrowAsync<InvalidOperationException>().WithMessage("once failed");

        q.Contains(once).Should().BeFalse();
        once.State.Should().Be(JobState.Suspended);
    }

    [Fact]
    public async Task Should_SkipAJob_WhenItWasSuspendedInTheRing()
    {
        var q = NewMain();
        var runs = 0;
        var job = Job.Cyclic("job", Work.Of(() => runs++));
        q.Add(job);
        job.Suspend();

        await FluentActions.Awaiting(() => q.ExecuteAsync(Ct)).Should().NotThrowAsync();
        runs.Should().Be(0);
        q.Contains(job).Should().BeTrue();

        job.Resume();
        await q.ExecuteAsync(Ct);
        runs.Should().Be(1);
    }

    [Fact]
    public void Should_RejectAJob_WhenItIsAlreadyInTheRing()
    {
        var q = NewMain();
        var job = Job.Cyclic("job", Work.Of(() => { }));
        q.Add(job);

        FluentActions.Invoking(() => q.Add(job)).Should().Throw<ArgumentException>();
    }
}
