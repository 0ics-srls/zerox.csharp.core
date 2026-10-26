namespace Zerox.Core.Messaging;

// The node a channel delivers its messages to.
public interface IMessageDestination<TKind>
    where TKind : struct, Enum
{
    bool CanAccept(Message<TKind> message);

    Task AcceptAsync(Message<TKind> message, CancellationToken cancellationToken);
}
