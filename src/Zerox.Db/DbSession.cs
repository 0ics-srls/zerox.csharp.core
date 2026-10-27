using System.Runtime.CompilerServices;
using Zerox.Core.Data;

namespace Zerox.Db;

// The state of ONE context instance. It lives and dies with the context: attached to it through a
// ConditionalWeakTable, so nothing has to register it and nothing keeps a context alive.
internal sealed class DbSession
{
    private static readonly ConditionalWeakTable<object, DbSession> Sessions = [];

    // The open root boundary, if any: while it is set, every save is swallowed.
    internal UnitOfWork? Boundary { get; private set; }

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

    // Leaving without applying is a choice, not an error: nothing reached the database.
    internal void CloseWithoutApplying(UnitOfWork who)
    {
        if (ReferenceEquals(who, Boundary))
            Boundary = null;
    }
}
