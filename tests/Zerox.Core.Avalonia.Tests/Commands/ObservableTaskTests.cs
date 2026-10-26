using Avalonia.Threading;
using Zerox.Core.Avalonia.Commands;

namespace Zerox.Core.Avalonia.Tests.Commands;

public sealed class ObservableTaskTests
{
    [Fact]
    public Task Should_FollowTheTask_WhenItRunsAndEnds() => HeadlessUi.RunAsync(async () =>
    {
        var observed = new ObservableTask();
        var names = new List<string?>();
        observed.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        var gate = new TaskCompletionSource();

        var observation = observed.ObserveAsync(gate.Task);
        observed.IsRunning.Should().BeTrue();
        observed.IsCompleted.Should().BeFalse();

        gate.SetResult();
        await observation;
        Dispatcher.UIThread.RunJobs();

        observed.IsSuccessfullyCompleted.Should().BeTrue();
        observed.IsRunning.Should().BeFalse();
        names.Should().Equal(null, null);
    });

    [Fact]
    public Task Should_KeepTheFailureInTheTask_WhenTheObservedTaskFails() => HeadlessUi.RunAsync(async () =>
    {
        var observed = new ObservableTask();
        var failing = Task.FromException(new InvalidOperationException("broken"));

        await observed.ObserveAsync(failing);

        observed.IsFaulted.Should().BeTrue();
        observed.Exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("broken");
        await FluentActions.Awaiting(() => observed.Task!).Should().ThrowAsync<InvalidOperationException>();
    });
}
