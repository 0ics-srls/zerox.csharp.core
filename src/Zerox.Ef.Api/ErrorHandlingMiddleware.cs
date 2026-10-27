using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Domain;

namespace Zerox.Ef.Api;

// The outermost catch of the application: what nobody expected becomes the 500 the client gets, logged.
// A conflict on the optimistic lock is an expected event (409, a warning); a request that cannot be
// executed is the client's mistake (400 with the reason, not an error of ours).
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

    // Written with the HTTP JSON options (AddZeroxJson): the code goes out as its name.
    private static Task WriteAsync(HttpContext context, HttpStatusCode status, ApiError error)
    {
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsJsonAsync(error);
    }
}
