namespace Zerox.Ef.Core;

// The boundary of an operation that writes more than one repository: inside it every save is
// swallowed, and ApplyAsync writes ONCE, with all the work. It is not a transaction: nothing reaches
// the database before ApplyAsync, so there is nothing to roll back and no connection held while the
// logic runs. Whoever saves inside does not know it is inside: services compose without knowing each
// other.
public interface IUnitOfWork : IAsyncDisposable
{
    // Writes all the work once. On a boundary opened inside another one (a slave) does nothing and
    // returns 0: the outermost one writes.
    Task<int> ApplyAsync(CancellationToken ct = default);
}
