using Zerox.Core.Data;

namespace Zerox.Db;

// The handle of a boundary: forwards to the session, which owns the state. It must be an object of
// its own: the context's DisposeAsync is the context's.
internal sealed class UnitOfWork(DbSession session, IDbContext db, bool slave) : IUnitOfWork
{
    private bool _closed;

    internal bool Slave { get; } = slave;

    public Task<int> ApplyAsync(CancellationToken ct = default)
    {
        var applied = session.ApplyAsync(this, db, ct);
        _closed = true;
        return applied;
    }

    public ValueTask DisposeAsync()
    {
        if (!_closed)
            session.CloseWithoutApplying(this);
        return ValueTask.CompletedTask;
    }
}
