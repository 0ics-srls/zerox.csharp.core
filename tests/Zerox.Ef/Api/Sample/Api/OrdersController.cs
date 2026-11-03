using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Api;

public sealed class OrdersController(IRepository<Order> repository, IOrderService orders) : EntityController<Order>(repository)
{
    // A massive operation behind an action: many rows updated without loading them.
    [HttpPost("close-before/{date}")]
    public async Task<IActionResult> CloseBefore(DateOnly date, CancellationToken ct)
    {
        var closed = await orders.CloseBeforeAsync(date, ct);
        return closed.IsSuccess ? Ok(new { Closed = closed.Value }) : Answer(closed);
    }
}
