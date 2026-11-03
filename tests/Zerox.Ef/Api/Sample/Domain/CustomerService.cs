using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api.Tests.Sample.Domain;

// The service of customers: the base's operations, and the merge and the rename, each run through RunAsync.
public sealed class CustomerService(IRepository<Customer> customers, IRepository<Order> orders)
    : EntityService<Customer>(customers), ICustomerService
{
    // The example of a unit of work: two repositories, one write.
    public Task<OpResult<Customer>> MergeIntoAsync(int id, int intoId, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            if (id == intoId)
                return OpResult.Invalid<Customer>("A customer cannot be merged into itself.");

            await using var work = Repository.OpenUnitOfWork();
            var from = await Repository.GetByIdAsync(id, c);
            var into = await Repository.GetByIdAsync(intoId, c);
            if (from is null || into is null)
                return OpResult.NotFound<Customer>();   // disposed without applying: nothing written

            foreach (var order in await orders.Query(tracked: true).Where(o => o.CustomerId == id).ToListAsync(c))
                order.Customer = into;                   // EF sets the key from the navigation
            await orders.SaveChangesAsync(c);           // swallowed: the unit of work writes

            Repository.Delete(from);
            await Repository.SaveChangesAsync(c);       // swallowed

            await work.ApplyAsync(c);                   // ONE write
            return OpResult.Deleted<Customer>();
        }, ct);

    // One field, at the version the client read, without reading the row: a stale one is a Conflict.
    public Task<OpResult<Customer>> RenameAsync(int id, string name, uint version, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            Repository.Update(new Customer { Id = id, Name = name, Version = version }, [nameof(Customer.Name)]);
            if (!await SaveUnlessMissingAsync(id, c))
                return OpResult.NotFound<Customer>();
            return OpResult.Ok(await Repository.Query().SingleAsync(x => x.Id == id, c));
        }, ct);
}
