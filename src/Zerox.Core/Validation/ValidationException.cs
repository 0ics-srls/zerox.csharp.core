namespace Zerox.Core.Validation;

// Validation errors that reach a caller as an exception.
public sealed class ValidationException : Exception
{
    public ValidationException()
    {
    }

    public ValidationException(string message)
        : base(message)
    {
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ValidationException(IReadOnlyList<object> errors)
        : base(string.Join(Environment.NewLine, errors ?? throw new ArgumentNullException(nameof(errors))))
        => Errors = errors;

    public IReadOnlyList<object> Errors { get; } = [];
}
