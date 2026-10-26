namespace Zerox.Core.Scheduling;

// A job's failure, reported by its queue (the scheduler is the supervisor of its jobs).
public sealed record JobError(string JobName, Exception Exception, DateTimeOffset At);
