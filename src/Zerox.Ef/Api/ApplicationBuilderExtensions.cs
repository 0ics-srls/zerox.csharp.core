using Microsoft.AspNetCore.Builder;

namespace Zerox.Ef.Api;

/// <summary>
/// The library's part of the request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Put it first in the pipeline: every error after it becomes a ProblemDetails (400, 409 or 500), and
    /// every error status answered without a body — an unknown route, a NotFound() — gets one.
    /// </summary>
    public static IApplicationBuilder UseZeroxErrorHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorHandlingMiddleware>().UseStatusCodePages();
}
