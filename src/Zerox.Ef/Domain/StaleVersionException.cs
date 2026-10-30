namespace Zerox.Ef.Domain;

/// <summary>
/// The row an action read has another version than the one the client read: the client decided on a row
/// that is no longer there. For an action that must read the current row before deciding (a state machine,
/// a rule on stored values): it compares the versions first and throws this. The API answers 409.
/// </summary>
public sealed class StaleVersionException : Exception
{
    /// <summary>
    /// A conflict with the generic reason.
    /// </summary>
    public StaleVersionException()
        : base("The row was changed after it was read.")
    {
    }

    /// <summary>
    /// A conflict with the reason.
    /// </summary>
    public StaleVersionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// A conflict with the reason and its cause.
    /// </summary>
    public StaleVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
