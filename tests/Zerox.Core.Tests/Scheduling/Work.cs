namespace Zerox.Core.Tests.Scheduling;

// A job's work in the tests: a plain step behind the async contract of the jobs.
internal static class Work
{
    public static Func<CancellationToken, Task> Of(Action step) => _ =>
    {
        step();
        return Task.CompletedTask;
    };
}
