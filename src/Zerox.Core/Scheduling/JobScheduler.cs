using System.Threading.Channels;

namespace Zerox.Core.Scheduling;

// Main ring (main jobs + slow queue + custom queues) and priority ring; a tick runs one element of each.
public sealed class JobScheduler
{
    public const string MainQueue = "MainQueue";
    public const string SlowQueue = "SlowQueue";
    public const string PriorityQueue = "PriorityQueue";
    public const string MainGroup = "MainGroup";
    public const string SlowGroup = "SlowGroup";
    public const string PriorityGroup = "PriorityGroup";

    private readonly Lock _lock = new();
    private readonly TimeProvider _time;
    private readonly Channel<JobError> _errors = Channel.CreateUnbounded<JobError>();
    private readonly JobQueue _main;
    private readonly JobQueue _priority;
    private readonly Dictionary<string, JobQueue> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (IJob Job, JobQueue Queue)> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(IJob Job, JobQueue Queue)>> _groups = new(StringComparer.Ordinal);

    public JobScheduler(string name, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Name = name;
        _time = timeProvider;
        _main = NewQueue(MainQueue);
        _priority = NewQueue(PriorityQueue);
        _main.Resume();
        _priority.Resume();
        _queues[MainQueue] = _main;
        _queues[PriorityQueue] = _priority;
        CreateJobQueue(SlowQueue);
    }

    public string Name { get; }

    public ChannelReader<JobError> Errors => _errors.Reader;

    public bool IsSuspended => _main.State == JobState.Suspended;

    // One element of the main ring, then one of the priority ring.
    public void Tick()
    {
        if (_main.State == JobState.Ready)
            _main.Execute();
        if (_priority.State == JobState.Ready)
            _priority.Execute();
    }

    public void Suspend()
    {
        _main.Suspend();
        _priority.Suspend();
    }

    public void Resume()
    {
        _main.Resume();
        _priority.Resume();
    }

    public void Reset()
    {
        lock (_lock)
        {
            foreach (var queue in _queues.Values)
                queue.Reset();
        }
    }

    public void CreateJobQueue(string name, params string[] groups)
    {
        var queue = NewQueue(name);
        lock (_lock)
        {
            if (!_queues.TryAdd(name, queue))
                throw new ArgumentException($"A queue named {name} exists", nameof(name));
        }
        AddJob(queue, MainQueue, [.. groups, MainGroup]);
    }

    public void CreateMainJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), MainQueue, [.. groups, MainGroup]);

    public void CreateSlowJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), SlowQueue, [.. groups, SlowGroup]);

    public void CreatePriorityJob(string name, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), PriorityQueue, [.. groups, PriorityGroup]);

    public void CreateCustomJob(string name, string queueName, Action action, params string[] groups) => AddJob(Job.Cyclic(name, action), queueName, groups);

    public void CreateMainOneTimeJob(string name, Action action) => AddJob(Job.Once(name, action), MainQueue);

    public void CreatePriorityOneTimeJob(string name, Action action) => AddJob(Job.Once(name, action), PriorityQueue);

    public void CreateCustomOneTimeJob(string name, string queueName, Action action) => AddJob(Job.Once(name, action), queueName);

    public void CreateMainOneTimeDelayedJob(string name, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), MainQueue);

    public void CreateMainOneTimeDelayedJob(string name, Action action, TimeSpan after) => CreateMainOneTimeDelayedJob(name, action, _time.GetUtcNow() + after);

    public void CreatePriorityOneTimeDelayedJob(string name, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), PriorityQueue);

    public void CreatePriorityOneTimeDelayedJob(string name, Action action, TimeSpan after) => CreatePriorityOneTimeDelayedJob(name, action, _time.GetUtcNow() + after);

    public void CreateCustomOneTimeDelayedJob(string name, string queueName, Action action, DateTimeOffset when) => AddJob(new DelayedJob(name, action, _time, when), queueName);

    public void CreateCustomOneTimeDelayedJob(string name, string queueName, Action action, TimeSpan after) => CreateCustomOneTimeDelayedJob(name, queueName, action, _time.GetUtcNow() + after);

    // Any job into a queue; a cyclic one is indexed by name and joins the groups, a one-time one leaves after running.
    public void AddJob(IJob job, string queueName, params string[] groups)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(groups);
        lock (_lock)
        {
            var queue = Queue(queueName);
            if (job.Kind == JobKind.Cyclic)
            {
                if (!_jobs.TryAdd(job.Name, (job, queue)))
                    throw new ArgumentException($"A job named {job.Name} exists", nameof(job));
                foreach (var group in groups)
                {
                    if (!_groups.TryGetValue(group, out var members))
                        _groups[group] = members = [];
                    members.Add((job, queue));
                }
            }
            queue.Add(job);
        }
    }

    public void SuspendJob(string name)
    {
        lock (_lock)
            Suspend(Indexed(name));
    }

    public void ResumeJob(string name)
    {
        lock (_lock)
            Resume(Indexed(name));
    }

    public void SuspendGroup(string name)
    {
        lock (_lock)
        {
            foreach (var member in Group(name))
                Suspend(member);
        }
    }

    public void ResumeGroup(string name)
    {
        lock (_lock)
        {
            foreach (var member in Group(name))
                Resume(member);
        }
    }

    private static void Suspend((IJob Job, JobQueue Queue) member) => member.Queue.Remove(member.Job);

    private static void Resume((IJob Job, JobQueue Queue) member)
    {
        if (!member.Queue.Contains(member.Job))
            member.Queue.Add(member.Job);
        else
            member.Job.Resume();
    }

    private JobQueue NewQueue(string name)
        => new(name, (job, error) => _errors.Writer.TryWrite(new JobError(job.Name, error, _time.GetUtcNow())));

    private JobQueue Queue(string name)
        => _queues.TryGetValue(name, out var queue) ? queue : throw new ArgumentException($"No queue named {name}", nameof(name));

    private (IJob Job, JobQueue Queue) Indexed(string name)
        => _jobs.TryGetValue(name, out var member) ? member : throw new ArgumentException($"No job named {name}", nameof(name));

    private List<(IJob Job, JobQueue Queue)> Group(string name)
        => _groups.TryGetValue(name, out var members) ? members : throw new ArgumentException($"No group named {name}", nameof(name));
}
