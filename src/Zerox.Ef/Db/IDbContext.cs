using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zerox.Ef.Db;

// What the library needs from a context. DbContext already has every member:
// a project writes `class MyContext : DbContext, IDbContext` and adds nothing.
public interface IDbContext
{
    ChangeTracker ChangeTracker { get; }

    DatabaseFacade Database { get; }

    DbSet<T> Set<T>() where T : class;

    EntityEntry<T> Entry<T>(T entity) where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
