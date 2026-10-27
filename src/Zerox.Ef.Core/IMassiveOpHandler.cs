using System.Linq.Expressions;

namespace Zerox.Ef.Core;

// Updates or deletes many rows without loading them. These writes reach the database at once and
// bypass the tracker, so they do not mix with tracked ones: the handler does not open with pending
// changes or inside a unit of work, and while it is open no repository can save. It has its own
// transaction, the only way to undo what is already on the database: without CommitAsync, dispose
// rolls back.
public interface IMassiveOpHandler<T> : IAsyncDisposable where T : class
{
    // Returns the number of rows updated.
    Task<int> UpdateWhereAsync(Expression<Func<T, bool>> where, Action<IFieldsToSet<T>> fields, CancellationToken ct = default);

    // Returns the number of rows deleted.
    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> where, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
