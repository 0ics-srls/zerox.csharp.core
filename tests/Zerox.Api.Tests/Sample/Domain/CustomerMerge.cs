using Microsoft.EntityFrameworkCore;
using Zerox.Api.Tests.Sample.Core;
using Zerox.Api.Tests.Sample.Db;
using Zerox.Core.Data;

namespace Zerox.Api.Tests.Sample.Domain;

// The example of a unit of work: two repositories, one write.
public sealed class CustomerMerge(IRepository<Customer> customers, IRepository<Order> orders) : ICustomerMerge
{
    public async Task<bool> MergeAsync(int fromId, int intoId, CancellationToken ct = default)
    {
        if (fromId == intoId)
            throw new ArgumentException("A customer cannot be merged into itself.", nameof(intoId));

        await using var work = customers.OpenUnitOfWork();
        var from = await customers.GetByIdAsync(fromId, ct);
        var into = await customers.GetByIdAsync(intoId, ct);
        if (from is null || into is null)
            return false;   // disposed without applying: nothing written

        foreach (var order in await orders.Query(tracked: true).Where(o => o.CustomerId == fromId).ToListAsync(ct))
            order.Customer = into;                   // EF sets the key from the navigation
        await orders.SaveChangesAsync(ct);           // swallowed: the unit of work writes

        customers.Delete(from);
        await customers.SaveChangesAsync(ct);        // swallowed

        await work.ApplyAsync(ct);                   // ONE write
        return true;
    }
}
