using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Tests;

// The UI thread is recognised by Avalonia's synchronization context: on the headless platform
// Dispatcher.UIThread.CheckAccess() answers true on any thread, so it proves nothing.
public sealed class HeadlessUiTests
{
    [Fact]
    public Task Should_RunOnTheUiThread_WhenTheBodyIsDispatchedThroughTheSession()
        => HeadlessUi.RunAsync(() =>
        {
            SynchronizationContext.Current.Should().BeOfType<AvaloniaSynchronizationContext>();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Should_NotRunOnTheUiThread_WhenTheTestBodyRunsDirectly()
    {
        await HeadlessUi.RunAsync(() => Task.CompletedTask);   // the session is started

        (SynchronizationContext.Current is AvaloniaSynchronizationContext).Should().BeFalse();   // null outside the session
    }

    [Fact]
    public async Task Should_ReachTheTestWithTheSameException_WhenTheUiBodyThrows()
    {
        var run = () => HeadlessUi.RunAsync(() => throw new InvalidOperationException("ui failed"));

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("ui failed");
    }

    [Fact]
    public Task Should_ContinueOnTheUiThread_WhenTheBodyAwaits()
        => HeadlessUi.RunAsync(async () =>
        {
            await Task.Yield();
            SynchronizationContext.Current.Should().BeOfType<AvaloniaSynchronizationContext>();
        });
}
