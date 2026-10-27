namespace Zerox.Ef.Core;

// Opt-in optimistic lock: Version is the concurrency token. Implementing the interface is all an
// entity does: the model marks it (ApplyOptimisticLock) and every save of a modified row increments
// it. A stale save throws DbUpdateConcurrencyException; no lock is ever taken on the database.
// Version travels with the entity to the client and back, so the conflict is seen even when the
// entity arrives detached.
public interface IHasOptimisticLock
{
    int Version { get; set; }
}
