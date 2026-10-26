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
