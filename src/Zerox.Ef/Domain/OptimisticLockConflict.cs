using Microsoft.EntityFrameworkCore;

namespace Zerox.Ef.Domain;

/// <summary>
/// Tells the conflict of the optimistic lock from the other failed saves. EF throws
/// <see cref="DbUpdateConcurrencyException"/> on every UPDATE or DELETE that hits no row, even without a token
/// (a row that does not exist): the conflict belongs only to the opt-in lock, a concurrency token on the failed
/// entry.
/// </summary>
public static class OptimisticLockConflict
{
    /// <summary>
    /// True when an entry of the failed save has a concurrency token: the row changed after it was read.
    /// </summary>
    public static bool Is(DbUpdateConcurrencyException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Entries.Any(entry => entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken));
    }
}
