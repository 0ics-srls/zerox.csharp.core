using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    /// Adds the row: 201 with its place. The Id is the database's (400 when the body carries one), and so
    /// is the row version.
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id != 0)
            throw new InvalidRequestException("The Id is given by the database: leave it out of a creation.");
        await Repository.AddAsync(entity, ct).ConfigureAwait(false);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    /// <summary>
    /// The row as the client sent it, at the version it read: only the editable fields are written (never the
    /// key, the navigations, the reserved fields or what the database generates), without reading the row
    /// first. 200 with the row as stored; 404 when it is missing; a row changed since the client read it
    /// answers 409 (the middleware).
    /// </summary>
    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (id != entity.Id)
            throw new InvalidRequestException("The id in the route and in the body differ.");
        Repository.Update(entity, EntityFields.Editable<T>());
        if (!await SaveUnlessMissingAsync(id, ct).ConfigureAwait(false))
            return NotFound();
        return Ok(await Repository.Query().SingleAsync(e => e.Id == id, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Deletes the row at the version the client read (?version=N, required when the entity has a row
    /// version): 204; 404 when it is missing; 409 when it changed since — a delete must not destroy a change
    /// the client never saw.
    /// </summary>
    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, [FromQuery] ulong? version, CancellationToken ct)
    {
        if (Repository.HasRowVersion && version is null)
            throw new InvalidRequestException($"{typeof(T).Name} has an optimistic lock: give the Version you read, as ?version=N.");
        if (!Repository.TryDeleteById(id, version))
            throw new InvalidRequestException($"{version} is not a version of {typeof(T).Name}: give the Version you read.");
        return await SaveUnlessMissingAsync(id, ct).ConfigureAwait(false) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Saves a client's write. An UPDATE or DELETE that hit no row fails the same way whether the row is
    /// missing or changed since the client read it; only then one read tells them apart: false when the row
    /// is missing (the caller answers 404), the conflict goes on when it exists (the middleware answers 409).
    /// </summary>
    protected async Task<bool> SaveUnlessMissingAsync(int id, CancellationToken ct)
    {
        try
        {
            await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await Repository.Query().AnyAsync(e => e.Id == id, ct).ConfigureAwait(false))
                throw;
            return false;
        }
    }

    /// <summary>
    /// The rows the load works on. A controller with its own view of the rows overrides it.
    /// </summary>
    protected virtual IQueryable<T> SearchSource() => Repository.Query();
}
