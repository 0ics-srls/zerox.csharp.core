namespace Zerox.Ef.Api;

/// <summary>
/// What went wrong, as a client switches on it: the "Code" of every ProblemDetails the API answers (in
/// JSON the member name), given by its status in one place (AddZeroxProblemDetails).
/// </summary>
public enum ApiErrorCode
{
    /// <summary>
    /// The request cannot be executed as written (400); the detail says why and the right form.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// Nothing answers at that address: no row with that id, or no route (404).
    /// </summary>
    NotFound,

    /// <summary>
    /// The row was changed by somebody else after the client read it (409): read it again.
    /// </summary>
    Conflict,

    /// <summary>
    /// Something nobody expected (500); the details are in the server's log, never in the answer.
    /// </summary>
    InternalError,
}
