using Avalonia;
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Threading;

/// <summary>Work that belongs to the UI thread: run at once on it, posted to the dispatcher from any other thread.
/// Where no application runs (a unit test with no UI at all), the work runs on the calling thread — nothing would
/// ever pump a posted action there.</summary>
public static class UiThread
{
    /// <summary>Runs the action on the UI thread.</summary>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
