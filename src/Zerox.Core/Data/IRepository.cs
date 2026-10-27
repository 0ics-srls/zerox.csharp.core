namespace Zerox.Core.Data;

// The port to a table. No EF type in it: a project's Core can reference it without pulling EF.
public interface IRepository<T> where T : class, IEntity
{
    // Not tracked: a read has no reason to keep rows watched. `tracked: true` serves one case:
    // changing a collection of children from the parent (removed rows are deleted only if the
    // parent is tracked).
    IQueryable<T> Query(bool tracked = false);

    // What this unit of work holds and is NOT on the database yet: the same row arriving twice in
    // one page of a sync must find the first one, which the database does not have yet.
    IEnumerable<T> Local();

    // Tracked: "give me that row, I may change it" — a change is then saved without Update.
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    // Attaches a detached instance and marks it modified.
    void Update(T entity);

    void Delete(T entity);

    void DeleteRange(IEnumerable<T> entities);

    // Writes. The caller does not need to know whether something above it decides when.
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
