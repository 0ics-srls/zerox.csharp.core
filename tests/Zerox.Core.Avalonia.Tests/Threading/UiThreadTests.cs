using Avalonia.Threading;
using Zerox.Core.Avalonia.Threading;

namespace Zerox.Core.Avalonia.Tests.Threading;

public sealed class UiThreadTests
{
    // the UI thread is recognised the headless-safe way (CheckAccess is true on any thread in some headless setups)
    private static bool OnUi => SynchronizationContext.Current is AvaloniaSynchronizationContext;

    [Fact]
    public Task Should_RunTheWorkAtOnce_WhenCalledOnTheUiThread() => HeadlessUi.RunAsync(async () =>
    {
        var ran = false;

        UiThread.Run(() => ran = true);

        ran.Should().BeTrue("the work runs at once on the UI thread");
        await Task.CompletedTask;
    });

    [Fact]
    public Task Should_RunTheWorkOnTheUiThread_WhenCalledFromAnotherThread() => HeadlessUi.RunAsync(async () =>
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caller = -1;

        await Task.Run(() =>
        {
            caller = Environment.CurrentManagedThreadId;
            UiThread.Run(() => done.TrySetResult(OnUi));
        });

        (await done.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue("the posted work runs on the UI thread");
        Environment.CurrentManagedThreadId.Should().NotBe(caller, "the work did not run on the calling thread");
    });
}
