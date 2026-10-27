using System.Linq.Expressions;

namespace Zerox.Ef.Core;

/// <summary>
/// Updates or deletes many rows without loading them. These writes reach the database at once and
/// bypass the tracker, so they do not mix with tracked ones: the handler does not open with pending
/// changes or inside a unit of work, and while it is open no repository can save. It has its own
/// transaction, the only way to undo what is already on the database: without CommitAsync, dispose
/// rolls back.
/// </summary>
public interface IMassiveOpHandler<T> : IAsyncDisposable where T : class
{
    /// <summary>
    /// Returns the number of rows updated.
    /// </summary>
    Task<int> UpdateWhereAsync(Expression<Func<T, bool>> where, Action<IFieldsToSet<T>> fields, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> where, CancellationToken ct = default);

    /// <summary>
    /// Commits the transaction and closes the exclusive mode: from here the repositories save again.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);
}
