using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zerox.Ef.Db;

// The rules applied to a context configured with UseZeroxSession(): on every save, and on every road
// to the database — only the async ones are open. ONE instance for every context: identical options in
// every scope, so EF does not build a service provider per scope.
internal sealed class SessionInterceptor : SaveChangesInterceptor, IDbCommandInterceptor, IDbConnectionInterceptor, IDbTransactionInterceptor
{
    internal static readonly SessionInterceptor Instance = new();

    private SessionInterceptor()
    {
    }

    /// <summary>
    /// The synchronous save would also bypass the rules of the session.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw Refused("Save only with SaveChangesAsync: the synchronous save would also bypass the unit of work.");

    /// <summary>
    /// A query read synchronously (ToList, Count, First, foreach on a query…). Only the roads of an
    /// application are watched: EF's own schema work (EnsureCreatedAsync, MigrateAsync) runs some
    /// commands synchronously inside its async methods, and those are EF's, not ours.
    /// </summary>
    public InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
        IsApplicationRoad(eventData) ? throw Refused("Read only asynchronously (ToListAsync, FirstOrDefaultAsync…).") : result;

    public InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) =>
        IsApplicationRoad(eventData) ? throw Refused("Read only asynchronously (CountAsync, AnyAsync, SumAsync…).") : result;

    public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
        IsApplicationRoad(eventData) ? throw Refused("Run commands only asynchronously (ExecuteSqlRawAsync, ExecuteUpdateAsync, ExecuteDeleteAsync…).") : result;

    public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        throw Refused("Open the connection only asynchronously (OpenConnectionAsync).");

    public InterceptionResult<DbTransaction> TransactionStarting(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result) =>
        throw Refused("Start a transaction only asynchronously (BeginTransactionAsync).");

    public InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) =>
        throw Refused("Commit only asynchronously (CommitAsync).");

    public InterceptionResult TransactionRollingBack(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) =>
        throw Refused("Roll back only asynchronously (RollbackAsync).");

    private static bool IsApplicationRoad(CommandEventData eventData) =>
        eventData.CommandSource is CommandSource.LinqQuery or CommandSource.SaveChanges or CommandSource.FromSqlQuery
            or CommandSource.ExecuteSqlRaw or CommandSource.ExecuteDelete or CommandSource.ExecuteUpdate;

    private static SyncDatabaseAccessException Refused(string how) =>
        new($"The database is used only asynchronously. {how}");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var session = DbSession.Of(eventData.Context!);

        if (session.Massive is not null)
            throw new InvalidOperationException("A massive operation is open: no repository can save until it is closed.");

        // Inside a unit of work the save is swallowed: the unit of work writes, once.
        if (session.Boundary is not null)
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));

        return ValueTask.FromResult(result);
    }
}
