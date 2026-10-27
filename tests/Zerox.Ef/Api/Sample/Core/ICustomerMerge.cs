namespace Zerox.Ef.Api.Tests.Sample.Core;

// The app's own contract: its Core holds only contracts (zerox's contracts are in Zerox.Core).
public interface ICustomerMerge
{
    // Moves every order of one customer to another and deletes the first, in ONE write.
    // False, and nothing written, when a customer does not exist.
    Task<bool> MergeAsync(int fromId, int intoId, CancellationToken ct = default);
}
