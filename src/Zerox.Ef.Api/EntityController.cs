using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

// The controller of an entity: the DevExtreme search (query, groups, projection, distinct values) and
// the CRUD, all over IRepository<T>. A project writes `class XController(IRepository<X> r) : EntityController<X>(r)`
// and puts its own [Authorize]: the base has none. Every action is virtual; one repository per action,
// no unit of work (whoever writes more than one repository opens the boundary).
[ApiController]
[Route("api/[controller]")]
public abstract class EntityController<T>(IRepository<T> repository) : ControllerBase where T : class, IEntity
{
    protected IRepository<T> Repository { get; } = repository;

    // POST because the options travel in the body; the meaning is a read. Filter, sort and paging run
    // on the server.
    [HttpPost("query")]
    public virtual async Task<ActionResult<DataSourceResult<T>>> Query([FromBody] DataSourceLoadOptionsBase? options, CancellationToken ct) =>
        Ok(await Search.RowsAsync(Repository.Query(), options ?? new DataSourceLoadOptionsBase(), ct).ConfigureAwait(false));

    [HttpPost("groups")]
    public virtual async Task<ActionResult<GroupsResult>> Groups([FromBody] DataSourceLoadOptionsBase options, CancellationToken ct) =>
        Ok(await Search.GroupsAsync(SearchSource(), options, ct).ConfigureAwait(false));

    [HttpPost("projection")]
    public virtual async Task<ActionResult<DataSourceResult<IDictionary<string, object?>>>> Projection(
        [FromBody] DataSourceLoadOptionsBase options, CancellationToken ct) =>
        Ok(await Search.ProjectionAsync(SearchSource(), options, ct).ConfigureAwait(false));

    // The column name comes from the URL: it is checked against the real properties before any query.
    [HttpGet("values/{column}")]
    public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, CancellationToken ct)
    {
        if (!DistinctTextValues.IsTextColumn<T>(column))
            throw new InvalidRequestException($"'{column}' is not a text column of {typeof(T).Name}.");
        return Ok(await DistinctTextValues.ValuesAsync(Repository.Query(), column, ct).ConfigureAwait(false));
    }

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<T>> GetById(int id, CancellationToken ct) =>
        await Repository.GetByIdAsync(id, ct).ConfigureAwait(false) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Repository.AddAsync(entity, ct).ConfigureAwait(false);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    // A stale Version in the body makes the save fail: the error middleware answers 409.
    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (id != entity.Id)
            return BadRequest(new ApiError(ApiErrorCode.InvalidRequest, "The id in the route and in the body differ."));
        Repository.Update(entity);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await Repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
            return NotFound();
        Repository.Delete(entity);
        await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    // The rows groups and projection work on. A controller with its own view of the rows overrides it.
    protected virtual IQueryable<T> SearchSource() => Repository.Query();
}
