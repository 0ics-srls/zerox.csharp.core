using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Storage;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

// The state of ONE context instance. It lives and dies with the context: attached to it through a
// ConditionalWeakTable, so nothing has to register it and nothing keeps a context alive.
internal sealed class DbSession
{
    private static readonly ConditionalWeakTable<object, DbSession> Sessions = [];

    // The open root boundary, if any: while it is set, every save is swallowed.
    internal UnitOfWork? Boundary { get; private set; }

    // The open massive operation, if any: while it is set, every save throws.
    internal object? Massive { get; private set; }

    // The transaction of the massive operation: the only one of the library, and it serves only it.
    private IDbContextTransaction? _massiveTransaction;

    internal static DbSession Of(object context) => Sessions.GetValue(context, static _ => new DbSession());

    internal IUnitOfWork OpenBoundary(IDbContext db)
    {
        if (Boundary is not null)
            return new UnitOfWork(this, db, slave: true);
        Boundary = new UnitOfWork(this, db, slave: false);
        return Boundary;
    }

    internal async Task<int> ApplyAsync(UnitOfWork who, IDbContext db, CancellationToken ct)
    {
        if (who.Slave)
            return 0;
        if (!ReferenceEquals(who, Boundary))
            throw new InvalidOperationException("This unit of work has already been applied or closed.");

        Boundary = null;   // from here the save writes
        try
        {
            return await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // The boundary is dead and its work is still in the tracker: without this, the next save
            // that passes would write it piecemeal, knowing nothing about it — half an operation on the
            // database, the very defect the boundary exists to remove.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    // Opens the exclusive mode. Not with pending changes (they would be bypassed), not inside a unit of
    // work (it would write at once what the unit of work holds), and it clears the tracker: after a
    // massive write the entities read before would be stale.
    internal async Task<IMassiveOpHandler<T>> OpenMassiveAsync<T>(IDbContext db, CancellationToken ct) where T : class
    {
        if (Massive is not null)
            throw new InvalidOperationException("A massive operation is already open.");
        if (Boundary is not null)
            throw new InvalidOperationException("A unit of work is open: massive writes do not mix with tracked ones.");
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("There are pending changes: save them before opening a massive operation.");

        db.ChangeTracker.Clear();
        _massiveTransaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var handler = new MassiveOpHandler<T>(this, db);
        Massive = handler;
        return handler;
    }

    internal async Task CommitMassiveAsync(object who, CancellationToken ct)
    {
        if (!ReferenceEquals(who, Massive))
            throw new InvalidOperationException("This massive operation has already been closed.");
        if (_massiveTransaction is not null)
            await _massiveTransaction.CommitAsync(ct).ConfigureAwait(false);
        await CloseMassiveAsync(who).ConfigureAwait(false);
    }

    // Closed without commit, it rolls back: from here the context saves again.
    internal async ValueTask CloseMassiveAsync(object who)
    {
        if (!ReferenceEquals(who, Massive))
            return;
        if (_massiveTransaction is not null)
        {
            await _massiveTransaction.DisposeAsync().ConfigureAwait(false);
            _massiveTransaction = null;
        }
        Massive = null;
    }

    // Leaving without applying is a choice, not an error: nothing reached the database.
    internal void CloseWithoutApplying(UnitOfWork who)
    {
        if (ReferenceEquals(who, Boundary))
            Boundary = null;
    }
}
