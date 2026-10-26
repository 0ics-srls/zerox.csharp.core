# Messaging

<block id="05-message-channel">
## TDDAB-5: Port the Point-to-Point Message Channel

<intro>
Delivers, in `src/Zerox.Core/Messaging`, the channel that passes data from one node to another
without each pair inventing its own way:
- `Message<TKind>` (sealed, `TKind : struct, Enum`): `Id` (a new `Guid`), `Kind`, `SourceId`
  (`HierId`), `Data` (`object?`), `Info` (`string`, "" by default); equal when the `Id` is equal
  (`IEquatable<Message<TKind>>`);
- `IMessageSource<TKind>`: `Task<Message<TKind>> ProduceMessageAsync(CancellationToken)`;
- `IMessageDestination<TKind>`: `bool CanAccept(Message<TKind>)` (a read-only question),
  `Task AcceptAsync(Message<TKind>, CancellationToken)`;
- `MessageChannel<TKind>` (sealed, derives from block 01's `NotifyingObject`): `Name`; source and
  destination held by WEAK references (a channel never keeps a node alive): `AttachSource`,
  `DetachSource`, `AttachDestination`, `DetachDestination` (null → `ArgumentNullException`),
  `Source` / `Destination` (null when detached or collected); one `CurrentMessage` in memory (user
  decision: no persistence), `HasMessage`, `DestinationCanAccept`;
  `ReceiveAsync()` → takes a message from the source, false without a source;
  `SendAsync()` → delivers the current message when there is a destination that accepts it, false
  otherwise (the old `Send` did nothing silently: the caller now knows); `SendAndClearAsync()` →
  sends, then clears in any case (the old behaviour), returns whether it was delivered; `Clear()`;
  a change of the current message notifies `CurrentMessage`, `HasMessage`, `DestinationCanAccept`;
  attaching or detaching the destination notifies `DestinationCanAccept`; an exception of the
  source or the destination reaches the caller (P9). Thread safe (one `Lock`).

Capability: old `IMessage<KT>`, `IJsonMessage<KT>`, `GenericMessage<KT>`, `IMessageSource<KT>`,
`IMessageDestination<KT>`, `IMessageChannelP2P<KT>`, `GenericMessageChannelP2P<KT>` in
`../../tmp/zerox/Zerox.Wpf.Core/Core/Messaging/MessagesContext.cs`; test `Test_ChannelP2P_attached`
in `../../tmp/zerox/Zerox.Wpf.XTest.Core/Core/Messaging/MessagingTest.cs` (its message carries a
`MsgData : PocoProxy<MsgDataStruct>`; ported with block 04's `PocoProxy`). Dropped: the
`PersistMessage`/`LoadMessage` hooks (no implementation; the load swallowed every exception),
`IJsonMessage`/`ISerializableIntoJson` (Newtonsoft: `Data` is an `object?`), the empty
`MessagesContext` and `IMessageContext`. Everything async: receive, send and accept are async.

Files: `src/Zerox.Core/Messaging/Message.cs`, `IMessageSource.cs`, `IMessageDestination.cs`,
`MessageChannel.cs`; tests `tests/Zerox.Core.Tests/Messaging/MessageChannelTests.cs`.
</intro>

<red>
- test: Test_ChannelP2P_attached — the old scenario: a new channel has no source, destination or message and cannot deliver; after attaching both still no message; the source's data name set to "pippo & pappo"; `ReceiveAsync` → `HasMessage`, `DestinationCanAccept`; destination detached → cannot deliver and `SendAsync` is false; attached again → `SendAsync` true and the destination's data name is "pippo & pappo"
- test: Should_ReportNotReceived_WhenThereIsNoSource — `ReceiveAsync()` is false, no message
- test: Should_ReportNotSent_WhenTheDestinationRefuses — a destination whose `CanAccept` is false: `SendAsync()` false, `AcceptAsync` never called, the message stays
- test: Should_ClearAfterSending_WhenSendAndClear — `SendAndClearAsync()` true and no message; with no destination false and still cleared
- test: Should_NotifyTheMessageProperties_WhenAMessageArrives — `ReceiveAsync` raises `CurrentMessage`, `HasMessage`, `DestinationCanAccept`; `AttachDestination` raises `DestinationCanAccept`
- test: Should_ForgetTheDestination_WhenItIsCollected — a destination created in a non-inlined helper and dropped: after `GC.Collect()` + `GC.WaitForPendingFinalizers()` `Destination` is null
- test: Should_CompareMessagesById_WhenTwoHaveTheSameContent — two messages with the same kind, source and data are not equal; a message equals itself
- test: Should_LetTheDestinationExceptionReachTheCaller_WhenAcceptFails — `AcceptAsync` throwing `InvalidOperationException`: `SendAsync` throws it
</red>

### Implementation

`Message.cs`:

```csharp
using Zerox.Core.Identity;

namespace Zerox.Core.Messaging;

// A message between two nodes; two messages are the same message only when their Id is the same.
public sealed class Message<TKind>(TKind kind, HierId sourceId, object? data = null, string info = "") : IEquatable<Message<TKind>>
    where TKind : struct, Enum
{
    public Guid Id { get; } = Guid.NewGuid();

    public TKind Kind { get; } = kind;

    public HierId SourceId { get; } = sourceId ?? throw new ArgumentNullException(nameof(sourceId));

    public object? Data { get; } = data;

    public string Info { get; } = info ?? throw new ArgumentNullException(nameof(info));

    public bool Equals(Message<TKind>? other) => other is not null && other.Id == Id;

    public override bool Equals(object? obj) => Equals(obj as Message<TKind>);

    public override int GetHashCode() => Id.GetHashCode();
}
```

`IMessageSource.cs` / `IMessageDestination.cs`:

```csharp
namespace Zerox.Core.Messaging;

// The node a channel takes its messages from.
public interface IMessageSource<TKind>
    where TKind : struct, Enum
{
    Task<Message<TKind>> ProduceMessageAsync(CancellationToken cancellationToken);
}

// The node a channel delivers its messages to.
public interface IMessageDestination<TKind>
    where TKind : struct, Enum
{
    bool CanAccept(Message<TKind> message);

    Task AcceptAsync(Message<TKind> message, CancellationToken cancellationToken);
}
```

`MessageChannel.cs`:

```csharp
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
```

<success>
- [ ] `MessageChannelTests` pass (8 tests)
- [ ] Green-gate BTLT passes — build + tests + lint + typecheck (configured commands, skip n/a)
</success>
</block>
