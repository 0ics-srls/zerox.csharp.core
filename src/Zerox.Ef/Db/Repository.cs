using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

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

    public void Update(T entity, IReadOnlyCollection<string> fields)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(fields);
        var entry = _db.Set<T>().Attach(entity);
        foreach (var name in fields)
        {
            var property = entry.Metadata.FindProperty(name)
                ?? throw new ArgumentException($"{typeof(T).Name} has no property '{name}'.", nameof(fields));
            // The key identifies the row; what the database generates is the database's.
            if (!property.IsKey() && property.ValueGenerated == ValueGenerated.Never)
                entry.Property(name).IsModified = true;
        }
    }

    public bool TryDeleteById(int id, ulong? versionReadByClient)
    {
        var stub = Activator.CreateInstance<T>();
        stub.Id = id;
        if (RowVersion() is { } rowVersion)
        {
            if (versionReadByClient is not ulong version)
                throw new ArgumentException($"{typeof(T).Name} has a row version: give the version the client read.", nameof(versionReadByClient));
            if (!TryRowVersion(version, rowVersion.ClrType, out var value))
                return false;
            var property = rowVersion.PropertyInfo ?? throw new InvalidOperationException(
                $"{typeof(T).Name}.{rowVersion.Name} is a shadow row version: the version the client read is set on a property of the entity.");
            property.SetValue(stub, value);
        }
        _db.Set<T>().Remove(stub);
        return true;
    }

    public bool HasRowVersion => RowVersion() is not null;

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _db.Set<T>().AddAsync(entity, ct).ConfigureAwait(false);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        _db.Set<T>().AddRangeAsync(entities, ct);

    public void Delete(T entity) => _db.Set<T>().Remove(entity);

    public void DeleteRange(IEnumerable<T> entities) => _db.Set<T>().RemoveRange(entities);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public IUnitOfWork OpenUnitOfWork() => DbSession.Of(_db).OpenBoundary(_db);

    public async Task<IMassiveOpHandler<T>> OpenMassiveAsync(CancellationToken ct = default)
    {
        // A retrying provider refuses the first write inside a transaction opened outside its strategy: said here, at once.
        if (_db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("The provider retries failed commands, and refuses a transaction opened outside its execution strategy: use RunMassiveAsync.");
        return await DbSession.Of(_db).OpenMassiveAsync<T>(_db, ct).ConfigureAwait(false);
    }

    public Task<TResult> RunMassiveAsync<TResult>(Func<IMassiveOpHandler<T>, CancellationToken, Task<TResult>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return _db.Database.CreateExecutionStrategy().ExecuteAsync(
            async attempt =>
            {
                var massive = await DbSession.Of(_db).OpenMassiveAsync<T>(_db, attempt).ConfigureAwait(false);
                await using (massive.ConfigureAwait(false))
                {
                    var result = await work(massive, attempt).ConfigureAwait(false);
                    await massive.CommitAsync(attempt).ConfigureAwait(false);
                    return result;
                }
            },
            ct);
    }

    public void DetachAll()
    {
        foreach (var entry in _db.ChangeTracker.Entries<T>().ToList())
            entry.State = EntityState.Detached;
    }

    // A row version as EF defines it (IsRowVersion, [Timestamp]); IDbContext exposes no Model: the entity
    // type comes from the set.
    private IProperty? RowVersion() =>
        _db.Set<T>().EntityType.GetProperties()
            .SingleOrDefault(p => p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate);

    // A number from the client in the row version's own type (uint, ulong or long: ApplyOptimisticLock refuses
    // any other); one that does not fit was never a version of this row.
    private static bool TryRowVersion(ulong version, Type type, out object value)
    {
        value = version;
        if (type == typeof(uint))
        {
            if (version > uint.MaxValue)
                return false;
            value = (uint)version;
        }
        else if (type == typeof(long))
        {
            if (version > long.MaxValue)
                return false;
            value = (long)version;
        }
        return true;
    }
}
