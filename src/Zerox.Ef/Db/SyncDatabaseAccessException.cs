namespace Zerox.Ef.Db;

// A synchronous road to the database was taken: a query read with ToList, a save, a command, a
// transaction or a connection opened without await. Everything against the database is async; the
// session refuses the rest, so the mistake is seen where it is made instead of blocking a thread.
public sealed class SyncDatabaseAccessException : Exception
{
    public SyncDatabaseAccessException()
        : base("The database is used only asynchronously.")
    {
    }

    public SyncDatabaseAccessException(string message)
        : base(message)
    {
    }

    public SyncDatabaseAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
