using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

// The top of every request: every exception ends here and none goes further, so a web process never
// dies of one request. What nobody expected is logged as an error with its stack and becomes the 500
// the client gets; it is never hidden. A conflict on the optimistic lock is an expected event (409, a
// warning); a request that cannot be executed is the client's mistake (400 with the reason); a client
// that went away is nobody's error. When the response has already started nothing more can be written:
// the connection is aborted.
internal sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            await WriteAsync(context, HttpStatusCode.Conflict,
                new ApiError(ApiErrorCode.Conflict, "The row was changed by another user.")).ConfigureAwait(false);
        }
        catch (InvalidRequestException e)
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, new ApiError(ApiErrorCode.InvalidRequest, e.Message)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request aborted by the client");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception");
            await WriteAsync(context, HttpStatusCode.InternalServerError,
                new ApiError(ApiErrorCode.InternalError, "An unexpected error occurred.")).ConfigureAwait(false);
        }
    }

    // EF throws DbUpdateConcurrencyException on every UPDATE/DELETE that hits 0 rows, even without a
    // token (updating a row that does not exist): the 409 belongs only to the opt-in lock.
    private static bool IsOptimisticLockConflict(DbUpdateConcurrencyException e) =>
        e.Entries.Any(entry => entry.Metadata.GetProperties().Any(p => p.IsConcurrencyToken));

    // Written with the HTTP JSON options (AddZeroxJson): the code goes out as its name. After the start
    // of the response the status cannot change: the client gets a cut response, never a wrong one.
    private static Task WriteAsync(HttpContext context, HttpStatusCode status, ApiError error)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return Task.CompletedTask;
        }
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsJsonAsync(error);
    }
}
