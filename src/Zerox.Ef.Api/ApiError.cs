namespace Zerox.Ef.Api;

// The body of every error the API answers: a code a client can switch on, and a message for a person.
public sealed record ApiError(ApiErrorCode Code, string Message);

public enum ApiErrorCode
{
    InvalidRequest,
    Conflict,
    InternalError,
}
