using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample.Core;

// The app's own contract for customers: the operations every entity has, and its own.
public interface ICustomerService : IEntityService<Customer>
{
    // Moves every order of one customer to another and deletes the first, in ONE write: Deleted; NotFound, and
    // nothing written, when a customer does not exist; Invalid when both are the same.
    Task<OpResult<Customer>> MergeIntoAsync(int id, int intoId, CancellationToken ct = default);

    // One field at the version the client read, without reading the row: Ok with the row as stored, NotFound,
    // Conflict when it changed since.
    Task<OpResult<Customer>> RenameAsync(int id, string name, uint version, CancellationToken ct = default);
}
