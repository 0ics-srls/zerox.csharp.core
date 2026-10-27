using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zerox.Db;

// The rules applied on every save of a context configured with UseZeroxSession(). ONE instance for
// every context: identical options in every scope, so EF does not build a service provider per scope.
internal sealed class SessionInterceptor : SaveChangesInterceptor
{
    internal static readonly SessionInterceptor Instance = new();

    private SessionInterceptor()
    {
    }

    // The synchronous door would bypass the rules of the session.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException("Save only with SaveChangesAsync: the synchronous save would bypass the unit of work.");
}
