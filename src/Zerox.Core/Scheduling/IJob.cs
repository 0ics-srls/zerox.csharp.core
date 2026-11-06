namespace Zerox.Core.Scheduling;

// What a ring holds: plain jobs and queues alike.
public interface IJob
{
    string Name { get; }

    JobKind Kind { get; }

    JobState State { get; }

    // False when it had nothing to do: a delayed job before its time, a job suspended (it does not run, and that is no failure).
    Task<bool> ExecuteAsync(CancellationToken cancellationToken);

    void Suspend();

    void Resume();
}
