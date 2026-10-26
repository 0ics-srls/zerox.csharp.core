using Avalonia.Threading;

namespace Zerox.Core.Avalonia.Commands;

// Progress whose handler runs on the UI thread, whatever thread reports.
internal sealed class UiProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => Dispatcher.UIThread.Post(() => handler(value));
}
