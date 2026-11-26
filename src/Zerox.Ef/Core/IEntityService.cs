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
    /// The distinct values of a text column, for the dropdown of its filter: of the rows the load works on, under
    /// the <see cref="LoadOptions.Filter"/> of the options — the options of the client's grid as they are; their
    /// sort, page, selection, grouping and summaries are not read. Each value comes with the number of those rows
    /// that have it. Ordered, without empty values, a bounded number.
    /// Invalid when the column is not a readable text column, or the filter cannot be run.
    /// </summary>
    Task<OpResult<IReadOnlyList<TextValue>>> ValuesAsync(string column, LoadOptions options, CancellationToken ct = default);

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
