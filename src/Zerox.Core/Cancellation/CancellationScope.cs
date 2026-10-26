namespace Zerox.Core.Cancellation;

// The root of the cancellation tree, one per application: CancelAll() stops every scope.
public sealed class Terminator(TimeProvider timeProvider) : IDisposable
{
    private readonly CancellationTokenSource _root = new();
    private readonly ScopeChildren _children = new();

    public CancellationToken Token => _root.Token;

    public int Count => _children.Count;

    public CancellationScope CreateScope(TimeSpan? timeout = null)
        => _children.Add(new CancellationScope(timeout, timeProvider, _children, _root.Token));

    // The exceptions of the callbacks reach the caller.
    public void CancelAll() => _root.Cancel();

    public void Dispose()
    {
        _children.DisposeAll();
        _root.Dispose();
    }
}

// A node of the tree: canceled by Cancel(), by its parent, or by its own timeout.
public sealed class CancellationScope : IDisposable
{
    private readonly CancellationToken _parent;
    private readonly CancellationTokenSource? _timeout;
    private readonly CancellationTokenSource _own;
    private readonly TimeProvider _timeProvider;
    private readonly ScopeChildren _siblings;
    private readonly ScopeChildren _children = new();

    internal CancellationScope(TimeSpan? timeout, TimeProvider timeProvider, ScopeChildren siblings, CancellationToken parent)
    {
        _parent = parent;
        _timeProvider = timeProvider;
        _siblings = siblings;
        _timeout = timeout is { } span ? new CancellationTokenSource(span, timeProvider) : null;
        _own = _timeout is null
            ? CancellationTokenSource.CreateLinkedTokenSource(parent)
            : CancellationTokenSource.CreateLinkedTokenSource(parent, _timeout.Token);
    }

    public CancellationToken Token => _own.Token;

    public int Count => _children.Count;

    // The parent comes first: a child of a canceled scope is canceled even if its own timeout elapsed.
    public CancellationState State
        => _parent.IsCancellationRequested ? CancellationState.Canceled
         : _timeout?.IsCancellationRequested == true ? CancellationState.TimedOut
         : _own.IsCancellationRequested ? CancellationState.Canceled
         : CancellationState.Active;

    public bool IsActive => State == CancellationState.Active;

    public CancellationScope CreateChild(TimeSpan? timeout = null)
        => _children.Add(new CancellationScope(timeout, _timeProvider, _children, _own.Token));

    // The exceptions of the callbacks reach the caller.
    public void Cancel() => _own.Cancel();

    public void ThrowIfCanceled() => _own.Token.ThrowIfCancellationRequested();

    public Task DelayAsync(TimeSpan delay) => Task.Delay(delay, _timeProvider, _own.Token);

    public void Dispose()
    {
        _children.DisposeAll();
        _siblings.Remove(this);
        _own.Dispose();
        _timeout?.Dispose();
    }
}

// The live children of a node; one lock, no retry loop.
internal sealed class ScopeChildren
{
    private readonly Lock _lock = new();
    private readonly HashSet<CancellationScope> _items = [];
    private bool _disposed;

    public int Count
    {
        get
        {
            lock (_lock)
                return _items.Count;
        }
    }

    public CancellationScope Add(CancellationScope scope)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                scope.Dispose();
                throw new ObjectDisposedException(nameof(CancellationScope), "The parent scope is disposed");
            }
            _items.Add(scope);
        }
        return scope;
    }

    public void Remove(CancellationScope scope)
    {
        lock (_lock)
            _items.Remove(scope);
    }

    public void DisposeAll()
    {
        CancellationScope[] items;
        lock (_lock)
        {
            _disposed = true;
            items = [.. _items];
        }
        foreach (var item in items)
            item.Dispose();
    }
}
