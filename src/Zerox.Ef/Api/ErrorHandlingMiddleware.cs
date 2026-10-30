using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

// The top of every request: every exception ends here and none goes further, so a web process never
// dies of one request. What nobody expected is logged as an error with its stack and becomes the 500
// the client gets; it is never hidden. A conflict on the optimistic lock is an expected event (409, a
// warning); a request that cannot be executed is the client's mistake (400 with the reason); a client
// that went away is nobody's error. Every answer is a ProblemDetails written by the platform's service
// (AddZeroxProblemDetails adds the code). When the response has already started nothing more can be
// written: the connection is aborted.
internal sealed class ErrorHandlingMiddleware(RequestDelegate next, IProblemDetailsService problems, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException e) when (IsOptimisticLockConflict(e))
        {
            logger.LogWarning(e, "Optimistic lock conflict");
            await WriteAsync(context, StatusCodes.Status409Conflict, "The row was changed by another user.").ConfigureAwait(false);
        }
        catch (StaleVersionException e)
        {
            logger.LogWarning(e, "Optimistic lock conflict");
            await WriteAsync(context, StatusCodes.Status409Conflict, "The row was changed by another user.").ConfigureAwait(false);
        }
        catch (InvalidRequestException e)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, e.Message).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request aborted by the client");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception");
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.").ConfigureAwait(false);
        }
    }

    // EF throws DbUpdateConcurrencyException on every UPDATE/DELETE that hits 0 rows, even without a
    // token (updating a row that does not exist): the 409 belongs only to the opt-in lock.
    private static bool IsOptimisticLockConflict(DbUpdateConcurrencyException e) =>
        e.Entries.Any(entry => entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken));

    // Written by the platform's ProblemDetails service with the HTTP JSON options (AddZeroxJson). After
    // the start of the response the status cannot change: the client gets a cut response, never a wrong one.
    private async Task WriteAsync(HttpContext context, int status, string detail)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Detail = detail },
        }).ConfigureAwait(false);
    }
}
