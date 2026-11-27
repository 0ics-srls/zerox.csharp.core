using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using Zerox.Core.Avalonia.Commands;
using Zerox.Core.Cancellation;

namespace Zerox.Core.Avalonia.Tests.Commands;

public sealed class AsyncCommandTests
{
    private static bool OnUi => SynchronizationContext.Current is AvaloniaSynchronizationContext;

    // Runs the posted jobs until the condition holds (bounded: a broken condition fails, it does not hang).
    private static void Drain(Func<bool> until)
    {
        for (var i = 0; i < 100 && !until(); i++)
            Dispatcher.UIThread.RunJobs();
        until().Should().BeTrue();
    }

    [Fact]
    public Task Should_ReportRunning_WhenTheWorkRuns() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var gate = new TaskCompletionSource();
        var command = AsyncCommand.Create(_ => gate.Task, terminator);

        var run = command.ExecuteAsync();
        command.CanExecute(null).Should().BeFalse();
        command.Execution.IsRunning.Should().BeTrue();

        gate.SetResult();
        (await run).Should().BeTrue();
        command.CanExecute(null).Should().BeTrue();
    });

    [Fact]
    public Task Should_NotStartTwice_WhenExecutedDuringItsRun() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var gate = new TaskCompletionSource();
        var runs = 0;
        var command = AsyncCommand.Create(_ =>
        {
            runs++;
            return gate.Task;
        }, terminator);

        var first = command.ExecuteAsync();
        (await command.ExecuteAsync()).Should().BeFalse();

        gate.SetResult();
        (await first).Should().BeTrue();
        runs.Should().Be(1);
    });

    [Fact]
    public Task Should_NotRun_WhenTheConfirmationSaysNo() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var ran = false;
        var command = new AsyncCommand<string>((_, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        }, terminator, confirm: _ => Task.FromResult(false));

        (await command.ExecuteAsync("a")).Should().BeFalse();

        ran.Should().BeFalse();
        command.CanExecute("a").Should().BeTrue();
    });

    [Fact]
    public Task Should_CancelTheWork_WhenTheCancelCommandRuns() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(scope => scope.DelayAsync(TimeSpan.FromHours(1)), terminator);

        var run = command.ExecuteAsync();
        command.CancelCommand.CanExecute(null).Should().BeTrue();
        command.CancelCommand.Execute(null);

        (await run).Should().BeTrue();
        command.Execution.IsCanceled.Should().BeTrue();
        command.IsTimedOut.Should().BeFalse();
        command.CancelCommand.CanExecute(null).Should().BeFalse();
    });

    [Fact]
    public Task Should_TimeOut_WhenTheTimeoutElapses() => HeadlessUi.RunAsync(async () =>
    {
        var clock = new FakeTimeProvider();
        using var terminator = new Terminator(clock);
        var command = AsyncCommand.Create(scope => scope.DelayAsync(TimeSpan.FromHours(1)), terminator, timeout: TimeSpan.FromSeconds(5));

        var run = command.ExecuteAsync();
        clock.Advance(TimeSpan.FromSeconds(5));

        (await run).Should().BeTrue();
        command.Execution.IsCanceled.Should().BeTrue();
        command.IsTimedOut.Should().BeTrue();
    });

    [Fact]
    public Task Should_LetTheFailureReachTheCaller_WhenTheWorkThrows() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("broken");
        }, terminator);

        await FluentActions.Awaiting(() => command.ExecuteAsync()).Should().ThrowAsync<InvalidOperationException>().WithMessage("broken");

        command.Execution.IsFaulted.Should().BeTrue();
        command.CanExecute(null).Should().BeTrue();
    });

    [Fact]
    public Task Should_LetTheFailureReachTheDispatcher_WhenRunFromABinding() => HeadlessUi.RunAsync(() =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("broken");
        }, terminator);
        Exception? seen = null;
        void Handler(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            seen = e.Exception;
            e.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += Handler;
        try
        {
            ((ICommand)command).Execute(null);
            Drain(() => seen is not null);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Handler;
        }

        seen.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("broken");
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_ReportProgressOnTheUiThread_WhenTheWorkReports() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var reported = new List<(int Value, bool OnUi)>();
        var command = AsyncCommand.WithProgress<string, int>(
            (_, _, progress) => Task.Run(() => progress.Report(5)),
            value => reported.Add((value, OnUi)),
            terminator);

        (await command.ExecuteAsync("a")).Should().BeTrue();
        Drain(() => reported.Count > 0);

        reported.Should().Equal((5, true));
    });

    [Fact]
    public Task Should_RaiseCanExecuteChanged_WhenTheRunStartsAndEnds() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(_ => Task.CompletedTask, terminator);
        var raised = new List<bool>();
        command.CanExecuteChanged += (_, _) => raised.Add(OnUi);

        await command.ExecuteAsync();
        Dispatcher.UIThread.RunJobs();

        raised.Should().HaveCountGreaterThanOrEqualTo(2).And.OnlyContain(onUi => onUi);
    });

    [Fact]
    public Task Should_RaiseCanExecuteChanged_AtOnce_WhenRaisedOnTheUiThread() => HeadlessUi.RunAsync(() =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(_ => Task.CompletedTask, terminator);
        var raised = false;
        command.CanExecuteChanged += (_, _) => raised = true;

        command.RaiseCanExecuteChanged();

        raised.Should().BeTrue("the requery follows the UI thread's rule: at once on it, posted nowhere");
        return Task.CompletedTask;
    });

    [Fact]
    public Task Should_RaiseCanExecuteChanged_OnTheUiThread_WhenRaisedFromAnotherThread() => HeadlessUi.RunAsync(async () =>
    {
        using var terminator = new Terminator(new FakeTimeProvider());
        var command = AsyncCommand.Create(_ => Task.CompletedTask, terminator);
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        command.CanExecuteChanged += (_, _) => done.TrySetResult(OnUi);

        await Task.Run(command.RaiseCanExecuteChanged);

        (await done.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue("the posted requery runs on the UI thread");
    });
}
