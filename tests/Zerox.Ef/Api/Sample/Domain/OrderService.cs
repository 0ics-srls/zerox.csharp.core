using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api.Tests.Sample.Domain;

// The service of orders: the base's operations, and the close, run through RunAsync.
public sealed class OrderService(IRepository<Order> orders) : EntityService<Order>(orders), IOrderService
{
    // The example of a massive operation: many rows updated without loading them.
    public Task<OpResult<int>> CloseBeforeAsync(DateOnly date, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            await using var massive = await Repository.OpenMassiveAsync(c);
            var closed = await massive.UpdateWhereAsync(
                o => o.Status == OrderStatus.Open && o.Date < date,
                fields => fields.Set(o => o.Status, OrderStatus.Closed),
                c);
            await massive.CommitAsync(c);
            return OpResult.Ok(closed);
        }, ct);
}
