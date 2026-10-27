using Microsoft.AspNetCore.Mvc;
using Zerox.Api.Tests.Sample.Db;
using Zerox.Core.Data;

namespace Zerox.Api.Tests.Sample.Api;

public sealed class OrdersController(IRepository<Order> repository) : EntityController<Order>(repository)
{
    // A massive operation behind an action: many rows updated without loading them.
    [HttpPost("close-before/{date}")]
    public async Task<IActionResult> CloseBefore(DateOnly date, CancellationToken ct)
    {
        await using var massive = await Repository.OpenMassiveAsync(ct);
        var closed = await massive.UpdateWhereAsync(
            o => o.Status == OrderStatus.Open && o.Date < date,
            fields => fields.Set(o => o.Status, OrderStatus.Closed),
            ct);
        await massive.CommitAsync(ct);
        return Ok(new { Closed = closed });
    }
}
