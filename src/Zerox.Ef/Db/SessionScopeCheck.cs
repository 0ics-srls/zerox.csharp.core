using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Ef.Db;

// Ends with the scope that owns the context. A unit of work or a massive operation still open there is
// an await using forgotten: the saves of the unit of work were swallowed and nothing was written, or the
// massive operation's transaction is still open. Both are closed first (nothing is left open behind),
// then the mistake is said where it was made instead of being lost. Cheap to resolve: it creates no context,
// it watches the one a repository asked for.
internal sealed class SessionScopeCheck : IAsyncDisposable
{
    private IDbContext? _db;

    public void Watch(IDbContext db) => _db = db;

    public async ValueTask ThrowIfOpenAsync()
    {
        if (_db is null)
            return;
        var forgotten = await DbSession.Of(_db).CloseForgottenAsync().ConfigureAwait(false);
        if (forgotten is not null)
            throw new InvalidOperationException(forgotten);
    }

    public ValueTask DisposeAsync() => ThrowIfOpenAsync();
}

/// <summary>
/// The check the end of a scope makes, for whoever must know before the scope ends: a web request has answered by then, and
/// its client would be told a write was done when nothing was written.
/// </summary>
public static class ZeroxSessionScope
{
    /// <summary>
    /// Closes a unit of work or a massive operation left open in the scope, and says so. Does nothing in a scope that opened
    /// no context, or that has no session.
    /// </summary>
    /// <param name="scope">The services of the scope.</param>
    /// <exception cref="InvalidOperationException">One was left open: an await using was forgotten.</exception>
    public static ValueTask ThrowIfOpenAsync(IServiceProvider scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.GetService<SessionScopeCheck>()?.ThrowIfOpenAsync() ?? ValueTask.CompletedTask;
    }
}
