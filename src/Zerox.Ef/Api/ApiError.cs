namespace Zerox.Ef.Api;

/// <summary>
/// The body of every error the API answers: a code a client can switch on, and a message for a person.
/// </summary>
public sealed record ApiError(ApiErrorCode Code, string Message);

/// <summary>
/// What went wrong, as a client switches on it (in JSON the member name).
/// </summary>
public enum ApiErrorCode
{
    /// <summary>
    /// The request cannot be executed as written (400); the message says why and the right form.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The row was changed by somebody else after the client read it (409): read it again.
    /// </summary>
    Conflict,

    /// <summary>
    /// Something nobody expected (500); the details are in the server's log, never in the answer.
    /// </summary>
    InternalError,
}
