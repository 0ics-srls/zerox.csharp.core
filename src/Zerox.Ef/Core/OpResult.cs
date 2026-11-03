namespace Zerox.Ef.Core;

/// <summary>
/// What became of an operation. It has no transport meaning: the API translates it into HTTP, an
/// in-process UI switches on it.
/// </summary>
public enum OpStatus
{
    /// <summary>
    /// Done; the value is the result.
    /// </summary>
    Ok,

    /// <summary>
    /// A new row exists; the value is it.
    /// </summary>
    Created,

    /// <summary>
    /// The row is gone; no value.
    /// </summary>
    Deleted,

    /// <summary>
    /// No row with that id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The row changed after the caller read it; the reason says so.
    /// </summary>
    Conflict,

    /// <summary>
    /// The request cannot be executed as given; the reason says why.
    /// </summary>
    Invalid,

    /// <summary>
    /// The domain refused it in the current state (a trigger the state does not accept); the reason says why.
    /// </summary>
    Failed,

    /// <summary>
    /// An exception nobody expected, cancellation included; it is inside, with its message as the reason.
    /// </summary>
    Faulted,
}

/// <summary>
/// The outcome of an operation: its status, its value, why it did not succeed, and the exception when one
/// happened — the caller never gets an exception, so nothing breaks the async flow. Built through
/// <see cref="OpResult"/>.
/// </summary>
/// <typeparam name="T">The type of the value a success carries.</typeparam>
/// <param name="Status">What became of the operation.</param>
/// <param name="Value">The result of Ok and Created; default otherwise.</param>
/// <param name="Reason">Why it did not succeed: Conflict, Invalid, Failed, and the message of Faulted.</param>
/// <param name="Exception">The exception of Faulted; null otherwise.</param>
public sealed record OpResult<T>(OpStatus Status, T? Value, string? Reason, Exception? Exception)
{
    /// <summary>
    /// Ok, Created or Deleted.
    /// </summary>
    public bool IsSuccess => Status is OpStatus.Ok or OpStatus.Created or OpStatus.Deleted;
}

/// <summary>
/// The ways to build an <see cref="OpResult{T}"/>: one per status.
/// </summary>
public static class OpResult
{
    /// <summary>
    /// Done, with its result.
    /// </summary>
    public static OpResult<T> Ok<T>(T value) => new(OpStatus.Ok, value, null, null);

    /// <summary>
    /// A new row, with it.
    /// </summary>
    public static OpResult<T> Created<T>(T value) => new(OpStatus.Created, value, null, null);

    /// <summary>
    /// The row is gone.
    /// </summary>
    public static OpResult<T> Deleted<T>() => new(OpStatus.Deleted, default, null, null);

    /// <summary>
    /// No row with that id.
    /// </summary>
    public static OpResult<T> NotFound<T>() => new(OpStatus.NotFound, default, null, null);

    /// <summary>
    /// The row changed after the caller read it.
    /// </summary>
    /// <exception cref="ArgumentException">The reason is null, empty or blank.</exception>
    public static OpResult<T> Conflict<T>(string reason) => WithReason<T>(OpStatus.Conflict, reason);

    /// <summary>
    /// The request cannot be executed as given.
    /// </summary>
    /// <exception cref="ArgumentException">The reason is null, empty or blank.</exception>
    public static OpResult<T> Invalid<T>(string reason) => WithReason<T>(OpStatus.Invalid, reason);

    /// <summary>
    /// The domain refused it in the current state.
    /// </summary>
    /// <exception cref="ArgumentException">The reason is null, empty or blank.</exception>
    public static OpResult<T> Failed<T>(string reason) => WithReason<T>(OpStatus.Failed, reason);

    /// <summary>
    /// An exception nobody expected, carried inside; its message is the reason.
    /// </summary>
    /// <exception cref="ArgumentNullException">The exception is null.</exception>
    public static OpResult<T> Faulted<T>(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(OpStatus.Faulted, default, exception.Message, exception);
    }

    private static OpResult<T> WithReason<T>(OpStatus status, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(status, default, reason, null);
    }
}
