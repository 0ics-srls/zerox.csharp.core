using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

// For an action that changes ONE field and must win even if somebody saved in between: on a conflict
// the row is read again and the change applied again, so the last writer wins. No lock is ever taken,
// no explicit transaction: only repeated saves.
public sealed class LastWriterWins(IRepositoryFactory repositories)
{
    // Attempts in all before the conflict reaches the caller (the API answers 409).
    public const int MaxAttempts = 3;

    // Returns the saved row, or null when it does not exist or is outside the scope (the caller answers
    // 404): then nothing is retried.
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

            // Only on the first attempt the original Version is the client's, so a stale client is
            // caught; from the second the fresh one is used — that is how the last writer wins.
            if (attempt == 0 && versionReadByClient is int read && entity is IHasOptimisticLock)
                repository.PinReadVersion(entity, read);

            await apply(entity, ct).ConfigureAwait(false);
            try
            {
                await repository.SaveChangesAsync(ct).ConfigureAwait(false);
                return entity;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts - 1)
            {
                // The one catch that goes on: retrying IS the purpose. The stale copy is thrown away so
                // the next attempt reads the database, not the instance in memory.
                repository.DetachAll();
            }
        }
    }
}
