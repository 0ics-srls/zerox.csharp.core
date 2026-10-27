namespace Zerox.Ef.Core;

/// <summary>
/// The port to a table. No EF type in it: a project's Core can reference it without pulling EF.
/// </summary>
public interface IRepository<T> where T : class, IEntity
{
    /// <summary>
    /// Not tracked: a read has no reason to keep rows watched. `tracked: true` serves one case:
    /// changing a collection of children from the parent (removed rows are deleted only if the
    /// parent is tracked).
    /// </summary>
    IQueryable<T> Query(bool tracked = false);

    /// <summary>
    /// What this unit of work holds and is NOT on the database yet: the same row arriving twice in
    /// one page of a sync must find the first one, which the database does not have yet.
    /// </summary>
    IEnumerable<T> Local();

    /// <summary>
    /// Tracked: "give me that row, I may change it" — the change is saved by SaveChangesAsync. There is no
    /// Update of a whole detached entity: it would write every column, the fields the client never saw too.
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Adds a new row, written by the next save; its Id is given by the database.
    /// </summary>
    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Adds several new rows, written by the next save.
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>
    /// Marks the row to delete, deleted by the next save.
    /// </summary>
    void Delete(T entity);

    /// <summary>
    /// Marks several rows to delete, deleted by the next save.
    /// </summary>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// Writes — or is swallowed, returning 0, when a unit of work is open above: then the unit of work
    /// writes. The caller does not know which, and does not need to.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens the boundary for an operation that writes more than one repository. Opened while another
    /// is open, it is a slave: the outermost one writes. Always <c>await using var uow = repository.OpenUnitOfWork();</c>:
    /// a unit of work left open is closed at the end of the scope, which then throws (its saves were swallowed).
    /// </summary>
    IUnitOfWork OpenUnitOfWork();

    /// <summary>
    /// Opens the exclusive mode to update or delete many rows without loading them. Refused with
    /// pending changes, inside a unit of work, or while another one is open. Always
    /// <c>await using var massive = await repository.OpenMassiveAsync();</c>: one left open is rolled back
    /// at the end of the scope, which then throws.
    /// </summary>
    Task<IMassiveOpHandler<T>> OpenMassiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the ORIGINAL Version to the one the client read, so the update compares against it and a
    /// stale client is caught. Assigning the property is not enough on a tracked entity: its original
    /// is the fresh value just read. For entities implementing IHasOptimisticLock.
    /// </summary>
    void PinReadVersion(T entity, int version);

    /// <summary>
    /// Detaches EVERY tracked row of this type, so the next read goes to the database. Not only the
    /// current one: a stale tracked copy would make the re-read return the instance in memory.
    /// </summary>
    void DetachAll();
}
