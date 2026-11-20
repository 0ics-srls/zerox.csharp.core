using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api;

/// <summary>
/// The base of every controller that answers the outcome of a service: it holds the one translation of an
/// <see cref="OpResult{T}"/> into HTTP (<see cref="Answer{TValue}"/>) and nothing else — no action, no route,
/// no service. The controller of an entity derives <see cref="EntityController{T}"/>, which adds the load and
/// the CRUD; a controller that is not the controller of an entity (an operation of its own, a composite
/// answer) derives this one, writes its own actions and route, and answers with the same translation.
/// </summary>
[ApiController]
public abstract class ResultController : ControllerBase
{
    /// <summary>
    /// The one translation of an outcome into HTTP: Ok 200 with the value, Created 201 with the value, Deleted
    /// 204, NotFound 404, Conflict 409, Invalid and Failed 400 — each failure a ProblemDetails with the reason and
    /// the code of its status; Faulted rethrows the exception it carries to the error middleware (logged, 500).
    /// </summary>
    protected ActionResult Answer<TValue>(OpResult<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Status switch
        {
            OpStatus.Ok => Ok(result.Value),
            OpStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
            OpStatus.Deleted => NoContent(),
            OpStatus.NotFound => NotFound(),
            OpStatus.Conflict => Problem(detail: result.Reason, statusCode: StatusCodes.Status409Conflict),
            OpStatus.Invalid or OpStatus.Failed => Problem(detail: result.Reason, statusCode: StatusCodes.Status400BadRequest),
            OpStatus.Faulted => Rethrow(result.Exception ?? new InvalidOperationException(result.Reason)),
            _ => throw new UnreachableException($"OpStatus {result.Status} has no answer."),
        };
    }

    // The middleware is the one place that logs an unexpected exception and answers 500: a Faulted outcome goes
    // there, with the stack of the place that threw it.
    private static ActionResult Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Throw(exception);
        throw new UnreachableException();
    }
}
