namespace Zerox.Core.Scheduling;

// What a ring holds: plain jobs and queues alike.
public interface IJob
{
    string Name { get; }

    JobKind Kind { get; }

    JobState State { get; }

    // False when it had nothing to do yet (a delayed job before its time).
    bool Execute();

    void Suspend();

    void Resume();
}
