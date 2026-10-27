using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zerox.Ef.Db;

/// <summary>
/// What the library needs from a context. DbContext already has every member:
/// a project writes `class MyContext : DbContext, IDbContext` and adds nothing.
/// </summary>
public interface IDbContext
{
    /// <summary>
    /// The tracked rows of the context (DbContext.ChangeTracker).
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// The database of the context: transactions of the massive operation (DbContext.Database).
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// The set of an entity type (DbContext.Set).
    /// </summary>
    DbSet<T> Set<T>() where T : class;

    /// <summary>
    /// The tracking entry of a row: its original values (DbContext.Entry).
    /// </summary>
    EntityEntry<T> Entry<T>(T entity) where T : class;

    /// <summary>
    /// Saves under the session rules: swallowed inside a unit of work, refused during a massive operation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
