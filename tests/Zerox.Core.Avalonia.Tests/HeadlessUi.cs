using Avalonia.Headless;

namespace Zerox.Core.Avalonia.Tests;

// Runs a test body on the thread of the headless UI (one session per test assembly, app from TestApp).
// Avalonia.Headless.XUnit 12.1.4 is built for xunit v3 3.x: with xunit v3 4.x the session is used directly.
internal static class HeadlessUi
{
    private static HeadlessUnitTestSession Session => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApp).Assembly);

    public static Task RunAsync(Func<Task> body)
        => Session.Dispatch(async () =>
        {
            await body();
            return true;
        }, TestContext.Current.CancellationToken);
}
