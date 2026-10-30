using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Api;

public sealed class CustomersController(IRepository<Customer> repository, ICustomerMerge merge)
    : EntityController<Customer>(repository)
{
    // A unit of work behind an action: 204, or 404 when a customer does not exist. A project's own action
    // declares its answers; the errors of the middleware are in the document already.
    [HttpPost("{id:int}/merge-into/{intoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeInto(int id, int intoId, CancellationToken ct) =>
        await merge.MergeAsync(id, intoId, ct) ? NoContent() : NotFound();

    // One field, at the version the client read, without reading the row: a stale one answers 409, like a full PUT.
    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        Repository.Update(new Customer { Id = id, Name = body.Name, Version = body.Version }, [nameof(Customer.Name)]);
        if (!await SaveUnlessMissingAsync(id, ct))
            return NotFound();
        return Ok(await Repository.Query().SingleAsync(c => c.Id == id, ct));
    }
}
