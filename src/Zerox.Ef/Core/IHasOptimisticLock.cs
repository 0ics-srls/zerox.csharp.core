namespace Zerox.Ef.Core;

/// <summary>
/// Opt-in optimistic lock: Version is the concurrency token. Implementing the interface is all an
/// entity does: the model marks it (ApplyOptimisticLock), an added row starts at 0 and every save of a
/// modified row increments it. A stale save throws DbUpdateConcurrencyException; no lock is ever taken on the database.
/// Version travels with the entity to the client and back, so the conflict is seen even when the
/// entity arrives detached.
/// </summary>
public interface IHasOptimisticLock
{
    /// <summary>
    /// The version the row had when it was read; the library writes it, a client only sends it back.
    /// </summary>
    int Version { get; set; }
}
