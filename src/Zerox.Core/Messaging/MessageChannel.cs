using Zerox.Core.ComponentModel;

namespace Zerox.Core.Messaging;

// One message in memory between a source and a destination held weakly (a channel never keeps a node alive).
public sealed class MessageChannel<TKind> : NotifyingObject
    where TKind : struct, Enum
{
    private readonly Lock _lock = new();
    private WeakReference<IMessageSource<TKind>>? _source;
    private WeakReference<IMessageDestination<TKind>>? _destination;
    private Message<TKind>? _current;

    public MessageChannel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public Message<TKind>? CurrentMessage
    {
        get
        {
            lock (_lock)
                return _current;
        }
    }

    public bool HasMessage => CurrentMessage is not null;

    public IMessageSource<TKind>? Source
    {
        get
        {
            lock (_lock)
                return _source is not null && _source.TryGetTarget(out var source) ? source : null;
        }
    }

    public IMessageDestination<TKind>? Destination
    {
        get
        {
            lock (_lock)
                return _destination is not null && _destination.TryGetTarget(out var destination) ? destination : null;
        }
    }

    public bool DestinationCanAccept => Destination is { } destination && CurrentMessage is { } message && destination.CanAccept(message);

    public void AttachSource(IMessageSource<TKind> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_lock)
            _source = new(source);
    }

    public void DetachSource()
    {
        lock (_lock)
            _source = null;
    }

    public void AttachDestination(IMessageDestination<TKind> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (_lock)
            _destination = new(destination);
        NotifyPropertyChanged(nameof(DestinationCanAccept));
    }

    public void DetachDestination()
    {
        lock (_lock)
            _destination = null;
        NotifyPropertyChanged(nameof(DestinationCanAccept));
    }

    // Takes a message from the source; false without a source.
    public async Task<bool> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (Source is not { } source)
            return false;
        SetCurrent(await source.ProduceMessageAsync(cancellationToken).ConfigureAwait(false));
        return true;
    }

    // Delivers the current message when a destination accepts it; false when nothing was delivered.
    public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
    {
        if (Destination is not { } destination || CurrentMessage is not { } message || !destination.CanAccept(message))
            return false;
        await destination.AcceptAsync(message, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Sends, then clears in any case.
    public async Task<bool> SendAndClearAsync(CancellationToken cancellationToken = default)
    {
        var sent = await SendAsync(cancellationToken).ConfigureAwait(false);
        Clear();
        return sent;
    }

    public void Clear() => SetCurrent(null);

    private void SetCurrent(Message<TKind>? message)
    {
        bool changed;
        lock (_lock)
        {
            changed = !Equals(_current, message);
            _current = message;
        }
        if (changed)
            NotifyPropertiesChanged(nameof(CurrentMessage), nameof(HasMessage), nameof(DestinationCanAccept));
    }
}
