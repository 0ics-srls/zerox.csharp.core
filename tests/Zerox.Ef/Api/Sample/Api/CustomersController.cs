using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api.Tests.Sample.Api;

public sealed class CustomersController(IRepository<Customer> repository, ICustomerMerge merge, LastWriterWins lastWriterWins)
    : EntityController<Customer>(repository)
{
    // A unit of work behind an action: 204, or 404 when a customer does not exist. A project's own action
    // declares its answers; the errors of the middleware are in the document already.
    [HttpPost("{id:int}/merge-into/{intoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeInto(int id, int intoId, CancellationToken ct) =>
        await merge.MergeAsync(id, intoId, ct) ? NoContent() : NotFound();

    // One field, at the Version the client read: a stale one answers 409, like a full PUT.
    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var saved = await lastWriterWins.SaveAsync<Customer>(id, _ => true,
            (customer, _) =>
            {
                customer.Name = body.Name;
                return Task.CompletedTask;
            },
            body.Version, ct);
        return saved is null ? NotFound() : Ok(saved);
    }
}
