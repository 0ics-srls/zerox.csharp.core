using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// For an action that changes ONE field. Two ways, chosen by the caller:
/// - with the Version the client read: the optimistic lock. The save goes through only if the row is
///   still at that Version; otherwise the client decided on a row that is no longer there, and the
///   conflict reaches it (the API answers 409) — never retried, never overwritten;
/// - without it (a change the server decides alone): on a conflict the row is read again and the change
///   applied again, so the last writer wins.
/// No lock is ever taken, no explicit transaction: only saves.
/// </summary>
public sealed class LastWriterWins(IRepositoryFactory repositories)
{
    /// <summary>
    /// Attempts in all before the conflict reaches the caller (the API answers 409).
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Returns the saved row, or null when it does not exist or is outside the scope (the caller answers
    /// 404): then nothing is retried.
    /// </summary>
    public async Task<T?> SaveAsync<T>(
        int id,
        Expression<Func<T, bool>> scope,
        Func<T, CancellationToken, Task> apply,
        int? versionReadByClient = null,
        CancellationToken ct = default) where T : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(apply);
        var repository = repositories.For<T>();
        for (var attempt = 0; ; attempt++)
        {
            // Tracked: the attempt changes the row and saves it, and on a conflict reads it again.
            var entity = await repository.Query(tracked: true).Where(scope)
                .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
            if (entity is null)
                return null;

            // The update compares against the Version the client read, not the one just read here.
            if (versionReadByClient is int read && entity is IHasOptimisticLock)
                repository.PinReadVersion(entity, read);

            await apply(entity, ct).ConfigureAwait(false);
            try
            {
                await repository.SaveChangesAsync(ct).ConfigureAwait(false);
                return entity;
            }
            catch (DbUpdateConcurrencyException) when (versionReadByClient is null && attempt < MaxAttempts - 1)
            {
                // The one catch that goes on, and only without a client Version: retrying IS the
                // purpose. The stale copy is thrown away so the next attempt reads the database, not the
                // instance in memory.
                repository.DetachAll();
            }
        }
    }
}
