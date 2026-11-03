using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

/// <summary>
/// The controller of an entity: HTTP over its <see cref="IEntityService{T}"/> — one load for rows, columns and
/// groups, distinct values, the CRUD at the version the client read. It never touches the storage: every action
/// asks the service and answers its outcome (<see cref="Answer{TValue}"/>). A project writes
/// `class XController(IEntityService&lt;X&gt; s) : EntityController&lt;X&gt;(s)` — or receives its derived
/// service — and puts its own [Authorize]: the base has none. Every action is virtual.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class EntityController<T>(IEntityService<T> service) : ControllerBase where T : class, IEntity
{
    /// <summary>
    /// The service of the entity, for the actions a derived controller adds.
    /// </summary>
    protected IEntityService<T> Service { get; } = service;

    /// <summary>
    /// The one search of the entity, POST because the options travel in the body; the meaning is a read.
    /// The options decide what comes back: rows, only the asked columns (Select), or keys, counts and sums
    /// per group (Group) — filter, sort, paging, grouping and summaries run on the server.
    /// </summary>
    [HttpPost("load")]
    public virtual async Task<ActionResult<Loaded>> Load([FromBody] LoadOptions? options, CancellationToken ct) =>
        Answer(await Service.LoadAsync(options ?? new LoadOptions(), ct).ConfigureAwait(false));

    /// <summary>
    /// The distinct values of a text column; the column name comes from the URL and is checked against the real
    /// properties before any query.
    /// </summary>
    [HttpGet("values/{column}")]
    public virtual async Task<ActionResult<IReadOnlyList<string>>> Values(string column, CancellationToken ct) =>
        Answer(await Service.ValuesAsync(column, ct).ConfigureAwait(false));

    /// <summary>
    /// The row with that Id, or 404.
    /// </summary>
    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<T>> GetById(int id, CancellationToken ct) =>
        Answer(await Service.GetAsync(id, ct).ConfigureAwait(false));

    /// <summary>
    /// Adds the row: 201 with its place. The Id is the database's (400 when the body carries one), and so
    /// is the row version.
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<T>> Create([FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var result = await Service.CreateAsync(entity, ct).ConfigureAwait(false);
        return result is { Status: OpStatus.Created, Value: { } created }
            ? CreatedAtAction(nameof(GetById), new { id = created.Id }, created)
            : Answer(result);
    }

    /// <summary>
    /// The row as the client sent it, at the version it read: only the editable fields are written (never the
    /// key, the navigations, the reserved fields or what the database generates), without reading the row
    /// first. 200 with the row as stored; 404 when it is missing; 409 when it changed since the client read it.
    /// </summary>
    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<T>> Update(int id, [FromBody] T entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (id != entity.Id)
            throw new InvalidRequestException("The id in the route and in the body differ.");
        return Answer(await Service.UpdateAsync(entity, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Deletes the row at the version the client read (?version=N, required when the entity has a row
    /// version): 204; 404 when it is missing; 409 when it changed since — a delete must not destroy a change
    /// the client never saw.
    /// </summary>
    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, [FromQuery] ulong? version, CancellationToken ct) =>
        Answer(await Service.DeleteAsync(id, version, ct).ConfigureAwait(false));

    /// <summary>
    /// The one translation of an outcome into HTTP: Ok 200 with the value, Created 201 with the value, Deleted
    /// 204, NotFound 404, Conflict 409, Invalid and Failed 400 — each failure a ProblemDetails with the reason and
    /// the code of its status; Faulted rethrows the exception it carries to the error middleware (logged, 500).
    /// </summary>
    protected ActionResult Answer<TValue>(OpResult<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            OpStatus.Ok => Ok(result.Value),
            OpStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
            OpStatus.Deleted => NoContent(),
            OpStatus.NotFound => NotFound(),
            OpStatus.Conflict => Problem(detail: result.Reason, statusCode: StatusCodes.Status409Conflict),
            OpStatus.Invalid or OpStatus.Failed => Problem(detail: result.Reason, statusCode: StatusCodes.Status400BadRequest),
            OpStatus.Faulted => Rethrow(result.Exception ?? new InvalidOperationException(result.Reason)),
            _ => throw new UnreachableException($"OpStatus {result.Status} has no answer."),
        };
    }

    // The middleware is the one place that logs an unexpected exception and answers 500: a Faulted outcome goes
    // there, with the stack of the place that threw it.
    private static ActionResult Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Throw(exception);
        throw new UnreachableException();
    }
}
