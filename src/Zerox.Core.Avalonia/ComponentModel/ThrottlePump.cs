using System.Collections.Concurrent;
using Avalonia.Threading;

namespace Zerox.Core.Avalonia.ComponentModel;

// One timer per (clock, interval), shared by every container with that interval: a tick flushes, on the
// UI thread, the containers that asked since the previous tick. Pumps live as long as the process.
internal sealed class ThrottlePump
{
    private static readonly Dictionary<(TimeProvider, TimeSpan), ThrottlePump> Pumps = [];
    private readonly ConcurrentQueue<PropertyContainer> _waiting = new();
    private readonly ITimer _timer;   // referenced here so the timer lives

    private ThrottlePump(TimeProvider time, TimeSpan interval)
        => _timer = time.CreateTimer(_ => Dispatcher.UIThread.Post(Tick), null, interval, interval);

    public static ThrottlePump For(TimeProvider time, TimeSpan interval)
    {
        lock (Pumps)
        {
            if (!Pumps.TryGetValue((time, interval), out var pump))
            {
                pump = new ThrottlePump(time, interval);
                Pumps.Add((time, interval), pump);
            }
            return pump;
        }
    }

    public void Enqueue(PropertyContainer container) => _waiting.Enqueue(container);

    // Only the containers waiting when the tick starts: one that asks again during the flush waits for the next tick.
    private void Tick()
    {
        var count = _waiting.Count;
        for (var i = 0; i < count && _waiting.TryDequeue(out var container); i++)
            container.Flush();
    }
}
