using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;

namespace Zerox.Ef.Api.Tests.Sample.Api;

public sealed class CustomersController(ICustomerService customers) : EntityController<Customer>(customers)
{
    // A unit of work behind an action: 204, 404 when a customer does not exist, 400 when both are the same.
    // A project's own action declares its answers; the 400 and the errors of the middleware are in the document
    // already (ZeroxOpenApi documents them on every operation).
    [HttpPost("{id:int}/merge-into/{intoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeInto(int id, int intoId, CancellationToken ct) =>
        Answer(await customers.MergeIntoAsync(id, intoId, ct));

    // One field, at the version the client read, without reading the row: a stale one answers 409, like a full PUT.
    [HttpPut("{id:int}/name")]
    public async Task<ActionResult<Customer>> Rename(int id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Answer(await customers.RenameAsync(id, body.Name, body.Version, ct));
    }
}
