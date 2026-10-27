namespace Zerox.Ef.Db;

// Ends with the scope that owns the context. A unit of work or a massive operation still open there is
// an await using forgotten: the saves of the unit of work were swallowed and nothing was written, or the
// massive operation's transaction is still open. Both are closed first (nothing is left open behind),
// then the mistake is said where it was made instead of being lost.
internal sealed class SessionScopeCheck(IDbContext db) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var forgotten = await DbSession.Of(db).CloseForgottenAsync().ConfigureAwait(false);
        if (forgotten is not null)
            throw new InvalidOperationException(forgotten);
    }
}
