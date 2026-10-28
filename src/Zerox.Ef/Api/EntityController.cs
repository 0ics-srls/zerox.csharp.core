using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

/// <summary>
/// The controller of an entity: the DevExtreme search (one load for rows, columns and groups; distinct values) and
/// the CRUD, all over IRepository&lt;T&gt;. A project writes `class XController(IRepository&lt;X&gt; r) : EntityController&lt;X&gt;(r)`
/// and puts its own [Authorize]: the base has none. Every action is virtual; one repository per action,
/// no unit of work (whoever writes more than one repository opens the boundary).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class EntityController<T>(IRepository<T> repository) : ControllerBase where T : class, IEntity
{
    /// <summary>
    /// The repository of the entity, for the actions a derived controller adds.
    /// </summary>
    protected IRepository<T> Repository { get; } = repository;

    /// <summary>
    /// The most rows one load reads; a controller with larger pages overrides it.
    /// </summary>
    protected virtual int MaxTake => Search.DefaultMaxTake;

    /// <summary>
    /// The one search of the entity, POST because the options travel in the body; the meaning is a read.
    /// The options decide what comes back: rows, only the asked columns (Select), or keys, counts and sums
    /// per group (Group) — filter, sort, paging, grouping and summaries run on the server.
    /// </summary>
    [HttpPost("load")]
    public virtual async Task<ActionResult<LoadResult>> Load([FromBody] DataSourceLoadOptionsBase? options, CancellationToken ct) =>
        Ok(await Search.LoadAsync(SearchSource(), options ?? new DataSourceLoadOptionsBase(), MaxTake, ct).ConfigureAwait(false));

    /// <summary>
    /// The column name comes from the URL: it is checked against the real properties before any query.
    /// </summary>
    [HttpGet("values/{column}")]
    public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, CancellationToken ct)
    {
        if (!DistinctTextValues.IsTextColumn<T>(column))
            throw new InvalidRequestException($"'{column}' is not a readable text column of {typeof(T).Name}.");
        return Ok(await DistinctTextValues.ValuesAsync(Repository.Query(), column, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// The row with that Id, or 404.
    /// </summary>
    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<T>> GetById(int id, CancellationToken ct) =>
        await Repository.GetByIdAsync(id, ct).ConfigureAwait(false) is { } entity ? Ok(entity) : NotFound();

    /// <summary>
    /// Adds the row: 201 with its place. The Id is the database's (400 when the body carries one); a row
    /// with the optimistic lock starts at Version 0.
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id != 0)
            return BadRequest(new ApiError(ApiErrorCode.InvalidRequest, "The Id is given by the database: leave it out of a creation."));
        await Repository.AddAsync(entity, ct).ConfigureAwait(false);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    /// <summary>
    /// The stored row takes the editable fields of the body (never the reserved ones, which the client
    /// never sees, nor the navigations), so only what changed is written. A missing row is a 404. A stale
    /// Version in the body makes the save fail: the error middleware answers 409.
    /// </summary>
    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (id != entity.Id)
            return BadRequest(new ApiError(ApiErrorCode.InvalidRequest, "The id in the route and in the body differ."));
        var stored = await Repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (stored is null)
            return NotFound();
        EntityFields.CopyEditable(entity, stored);
        if (entity is IHasOptimisticLock sent)
            Repository.PinReadVersion(stored, sent.Version);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(stored);
    }

    /// <summary>
    /// A row with the optimistic lock is deleted only at the Version the client read (?version=N): a
    /// stale one answers 409, a missing one 400 — a delete must not destroy a change the client never saw.
    /// </summary>
    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, [FromQuery] int? version, CancellationToken ct)
    {
        var entity = await Repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
            return NotFound();
        if (entity is IHasOptimisticLock)
        {
            if (version is not int read)
                return BadRequest(new ApiError(ApiErrorCode.InvalidRequest,
                    $"{typeof(T).Name} has an optimistic lock: give the Version you read, as ?version=N."));
            Repository.PinReadVersion(entity, read);
        }
        Repository.Delete(entity);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// The rows the load works on. A controller with its own view of the rows overrides it.
    /// </summary>
    protected virtual IQueryable<T> SearchSource() => Repository.Query();
}
