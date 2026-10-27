using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

// The rules applied on every save of a context configured with UseZeroxSession(). ONE instance for
// every context: identical options in every scope, so EF does not build a service provider per scope.
internal sealed class SessionInterceptor : SaveChangesInterceptor
{
    internal static readonly SessionInterceptor Instance = new();

    private SessionInterceptor()
    {
    }

    // The synchronous door would bypass the rules of the session.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException("Save only with SaveChangesAsync: the synchronous save would bypass the unit of work.");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var session = DbSession.Of(eventData.Context!);

        if (session.Massive is not null)
            throw new InvalidOperationException("A massive operation is open: no repository can save until it is closed.");

        // Inside a unit of work the save is swallowed: the unit of work writes, once.
        if (session.Boundary is not null)
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));

        // The save goes through: every modified row with the lock counts one more version (an added
        // row starts at 0). EF detects the changes after this, so the new Version is written.
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<IHasOptimisticLock>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.Version++;
        }

        return ValueTask.FromResult(result);
    }
}
