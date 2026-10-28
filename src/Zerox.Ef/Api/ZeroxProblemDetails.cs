using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Ef.Api;

/// <summary>
/// One error format, the platform's: every failure the API answers — the error middleware's and the
/// framework's own (an unreadable body, a failed binding, an unknown route) — is a ProblemDetails, with
/// the code of the domain as its "Code".
/// </summary>
public static class ZeroxProblemDetails
{
    /// <summary>
    /// The member of a ProblemDetails that carries the code of the domain.
    /// </summary>
    public const string CodeMember = "Code";

    /// <summary>
    /// The ProblemDetails service of the platform, with the code of the domain added to every problem.
    /// </summary>
    public static IServiceCollection AddZeroxProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (CodeOf(context.ProblemDetails.Status) is { } code)
                context.ProblemDetails.Extensions.TryAdd(CodeMember, code.ToString());
        });
    }

    /// <summary>
    /// The code of the domain for a status, in one place: a status the API answers without a code here is
    /// left without one.
    /// </summary>
    public static ApiErrorCode? CodeOf(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ApiErrorCode.InvalidRequest,
        StatusCodes.Status404NotFound => ApiErrorCode.NotFound,
        StatusCodes.Status409Conflict => ApiErrorCode.Conflict,
        StatusCodes.Status500InternalServerError => ApiErrorCode.InternalError,
        _ => null,
    };
}
