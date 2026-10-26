namespace Zerox.Core.Messaging;

// The node a channel takes its messages from.
public interface IMessageSource<TKind>
    where TKind : struct, Enum
{
    Task<Message<TKind>> ProduceMessageAsync(CancellationToken cancellationToken);
}
