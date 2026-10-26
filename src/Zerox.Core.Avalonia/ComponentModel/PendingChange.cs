namespace Zerox.Core.Avalonia.ComponentModel;

// One property waiting for the pump: how many changes, how many sent, when the last one arrived.
internal sealed class PendingChange
{
    private long _changes;
    private long _sent;
    private long _lastChange;

    public bool HasUnsent => Interlocked.Read(ref _sent) < Interlocked.Read(ref _changes);

    public void Mark(long timestamp)
    {
        Interlocked.Exchange(ref _lastChange, timestamp);
        Interlocked.Increment(ref _changes);
    }

    // Due when it has unsent changes and has been quiet for the debounce (none: at once).
    public bool IsDue(TimeProvider time, TimeSpan debounce)
        => HasUnsent && (debounce == TimeSpan.Zero || time.GetElapsedTime(Interlocked.Read(ref _lastChange)) >= debounce);

    public void Commit() => Interlocked.Exchange(ref _sent, Interlocked.Read(ref _changes));
}
