namespace Zerox.Ef.Core;

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

    // Tracked: "give me that row, I may change it" — the change is saved by SaveChangesAsync. There is no
    // Update of a whole detached entity: it would write every column, the fields the client never saw too.
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    void Delete(T entity);

    void DeleteRange(IEnumerable<T> entities);

    // Writes — or is swallowed, returning 0, when a unit of work is open above: then the unit of work
    // writes. The caller does not know which, and does not need to.
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    // Opens the boundary for an operation that writes more than one repository. Opened while another
    // is open, it is a slave: the outermost one writes.
    IUnitOfWork OpenUnitOfWork();

    // Opens the exclusive mode to update or delete many rows without loading them. Refused with
    // pending changes, inside a unit of work, or while another one is open.
    Task<IMassiveOpHandler<T>> OpenMassiveAsync(CancellationToken ct = default);

    // Sets the ORIGINAL Version to the one the client read, so the update compares against it and a
    // stale client is caught. Assigning the property is not enough on a tracked entity: its original
    // is the fresh value just read. For entities implementing IHasOptimisticLock.
    void PinReadVersion(T entity, int version);

    // Detaches EVERY tracked row of this type, so the next read goes to the database. Not only the
    // current one: a stale tracked copy would make the re-read return the instance in memory.
    void DetachAll();
}
