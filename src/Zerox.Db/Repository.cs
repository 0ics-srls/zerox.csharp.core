using Microsoft.EntityFrameworkCore;
using Zerox.Core.Data;

namespace Zerox.Db;

// The port, written once: it decides nothing and forwards to the context.
internal sealed class Repository<T> : IRepository<T> where T : class, IEntity
{
    private readonly IDbContext _db;

    public Repository(IDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        // Without the session nothing would swallow the saves inside a unit of work, silently.
        if (!DbContextOptionsBuilderExtensions.HasZeroxSession(db))
            throw new InvalidOperationException(
                "The context is not configured with UseZeroxSession(): the unit of work would not swallow the saves. " +
                "Register the context with AddZeroxDb, or build its options with UseZeroxSession().");
        _db = db;
    }

    public IQueryable<T> Query(bool tracked = false) => tracked ? _db.Set<T>() : _db.Set<T>().AsNoTracking();

    public IEnumerable<T> Local() => _db.Set<T>().Local;

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _db.Set<T>().FindAsync([id], ct).ConfigureAwait(false);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _db.Set<T>().AddAsync(entity, ct).ConfigureAwait(false);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        _db.Set<T>().AddRangeAsync(entities, ct);

    public void Update(T entity) => _db.Set<T>().Update(entity);

    public void Delete(T entity) => _db.Set<T>().Remove(entity);

    public void DeleteRange(IEnumerable<T> entities) => _db.Set<T>().RemoveRange(entities);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public IUnitOfWork OpenUnitOfWork() => DbSession.Of(_db).OpenBoundary(_db);

    public Task<IMassiveOpHandler<T>> OpenMassiveAsync(CancellationToken ct = default) =>
        DbSession.Of(_db).OpenMassiveAsync<T>(_db, ct);
}
