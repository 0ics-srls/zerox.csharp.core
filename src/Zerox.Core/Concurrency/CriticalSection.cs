namespace Zerox.Core.Concurrency;

// Mutual exclusion that holds across await (SemaphoreSlim is not bound to a thread, unlike lock/Monitor).
public sealed class CriticalSection : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CriticalSectionLock Lock()
    {
        _gate.Wait();
        return new CriticalSectionLock(_gate);
    }

    public CriticalSectionLock Lock(TimeSpan timeout)
        => _gate.Wait(timeout) ? new CriticalSectionLock(_gate) : throw new TimeoutException($"Critical section not acquired in {timeout}");

    // A handle with IsLocked false when the section was not acquired in time.
    public CriticalSectionLock TryLock(TimeSpan timeout)
        => _gate.Wait(timeout) ? new CriticalSectionLock(_gate) : default;

    public async ValueTask<CriticalSectionLock> LockAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new CriticalSectionLock(_gate);
    }

    public async ValueTask<CriticalSectionLock> LockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => await _gate.WaitAsync(timeout, cancellationToken).ConfigureAwait(false)
            ? new CriticalSectionLock(_gate)
            : throw new TimeoutException($"Critical section not acquired in {timeout}");

    // A handle with IsLocked false when the section was not acquired in time.
    public async ValueTask<CriticalSectionLock> TryLockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => await _gate.WaitAsync(timeout, cancellationToken).ConfigureAwait(false) ? new CriticalSectionLock(_gate) : default;

    public void Dispose() => _gate.Dispose();
}

// The held section; disposing it releases the section. default = not acquired (IsLocked false).
public readonly struct CriticalSectionLock : IDisposable, IEquatable<CriticalSectionLock>
{
    private readonly SemaphoreSlim? _gate;

    internal CriticalSectionLock(SemaphoreSlim gate) => _gate = gate;

    public bool IsLocked => _gate is not null;

    public void Dispose() => _gate?.Release();

    public bool Equals(CriticalSectionLock other) => ReferenceEquals(_gate, other._gate);

    public override bool Equals(object? obj) => obj is CriticalSectionLock other && Equals(other);

    public override int GetHashCode() => _gate is null ? 0 : _gate.GetHashCode();

    public static bool operator ==(CriticalSectionLock left, CriticalSectionLock right) => left.Equals(right);

    public static bool operator !=(CriticalSectionLock left, CriticalSectionLock right) => !left.Equals(right);
}
