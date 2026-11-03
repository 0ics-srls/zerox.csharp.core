using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Core;

// The app's own contract for orders: the operations every entity has, and its own.
public interface IOrderService : IEntityService<Order>
{
    // Closes every open order dated before the day, without loading them: Ok with how many.
    Task<OpResult<int>> CloseBeforeAsync(DateOnly date, CancellationToken ct = default);
}
