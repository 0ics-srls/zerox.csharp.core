namespace Zerox.Ef.Domain;

// A request that cannot be executed, with the reason written for whoever built it: whoever builds the
// load options by hand (a client, an agent) cannot correct them without knowing why they are wrong.
// The API turns it into a 400 with the message.
public sealed class InvalidRequestException : Exception
{
    public InvalidRequestException()
        : base("Invalid request.")
    {
    }

    public InvalidRequestException(string message)
        : base(message)
    {
    }

    public InvalidRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
