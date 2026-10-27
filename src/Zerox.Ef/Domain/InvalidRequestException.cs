namespace Zerox.Ef.Domain;

/// <summary>
/// A request that cannot be executed, with the reason written for whoever built it: whoever builds the
/// load options by hand (a client, an agent) cannot correct them without knowing why they are wrong.
/// The API turns it into a 400 with the message.
/// </summary>
public sealed class InvalidRequestException : Exception
{
    /// <summary>
    /// A refusal with the generic reason.
    /// </summary>
    public InvalidRequestException()
        : base("Invalid request.")
    {
    }

    /// <summary>
    /// A refusal with the reason and the right form, for whoever built the request.
    /// </summary>
    public InvalidRequestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// A refusal with the reason and the exception of DevExtreme or of the database that caused it.
    /// </summary>
    public InvalidRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
