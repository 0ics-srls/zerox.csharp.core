using Microsoft.AspNetCore.Builder;

namespace Zerox.Api;

public static class ApplicationBuilderExtensions
{
    // Put it first in the pipeline: every error after it becomes an ApiError (400, 409 or 500).
    public static IApplicationBuilder UseZeroxErrorHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorHandlingMiddleware>();
}
