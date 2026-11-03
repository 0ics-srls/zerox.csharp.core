namespace Zerox.Ef.Core;

/// <summary>
/// The operations every entity has, for any caller — the API, an in-process UI. An outcome is never an
/// exception: every one, an unexpected exception included, comes back as an <see cref="OpResult{T}"/>. Only a
/// misuse (a null argument) throws, before anything runs.
/// </summary>
/// <typeparam name="T">The entity.</typeparam>
public interface IEntityService<T> where T : class, IEntity
{
    /// <summary>
    /// The one search: rows, only the asked columns, or groups, as the options decide. Invalid, with the reason,
    /// when the options cannot run.
    /// </summary>
    Task<OpResult<Loaded>> LoadAsync(LoadOptions options, CancellationToken ct = default);

    /// <summary>
    /// The distinct values of a readable text column, for a filter dropdown. Invalid for any other name.
    /// </summary>
    Task<OpResult<IReadOnlyList<string>>> ValuesAsync(string column, CancellationToken ct = default);

    /// <summary>
    /// The row with that id: Ok, or NotFound.
    /// </summary>
    Task<OpResult<T>> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Adds the row: Created with it, its Id and row version given by the database. Invalid when it carries an Id.
    /// </summary>
    Task<OpResult<T>> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Writes the editable fields of the caller's row at the version it read, without reading the row first:
    /// Ok with the row as stored, NotFound when it is missing, Conflict when it changed since.
    /// </summary>
    Task<OpResult<T>> UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Deletes the row at the version the caller read: Deleted, NotFound when it is missing, Conflict when it
    /// changed since; Invalid when a locked entity gets no version, or one that does not fit its row version.
    /// </summary>
    Task<OpResult<T>> DeleteAsync(int id, ulong? version, CancellationToken ct = default);
}
