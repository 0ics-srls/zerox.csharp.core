namespace Zerox.Ef.Db;

/// <summary>
/// A synchronous road to the database was taken: a query read with ToList, a save, a command, a
/// transaction or a connection opened without await. Everything against the database is async; the
/// session refuses the rest, so the mistake is seen where it is made instead of blocking a thread.
/// </summary>
public sealed class SyncDatabaseAccessException : Exception
{
    /// <summary>
    /// The refusal with the generic reason.
    /// </summary>
    public SyncDatabaseAccessException()
        : base("The database is used only asynchronously.")
    {
    }

    /// <summary>
    /// The refusal with the road taken and the async one to use instead.
    /// </summary>
    public SyncDatabaseAccessException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// The refusal with the exception that carried it.
    /// </summary>
    public SyncDatabaseAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
