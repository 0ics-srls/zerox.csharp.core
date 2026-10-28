using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Ef.Api;

/// <summary>
/// The JSON "as is": the names of the entities travel exactly as they are written (no camelCase), enums
/// as their names, days as yyyy-MM-dd, numbers as numbers — the same in controllers, minimal APIs and
/// OpenAPI.
/// </summary>
public static class ZeroxJson
{
    /// <summary>
    /// The JSON as is for the controllers and for the HTTP JSON options (minimal APIs, results, errors).
    /// </summary>
    public static IServiceCollection AddZeroxJson(this IMvcBuilder mvc)
    {
        ArgumentNullException.ThrowIfNull(mvc);
        mvc.AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy = null;
            AddConverters(o.JsonSerializerOptions);
        });
        // JSON only: application/json for the answers, application/problem+json for the errors (through
        // application/*+json). Plain text and text/json would be one more answer for every action of the
        // document, and one more variant of every call of a generated client.
        mvc.AddMvcOptions(o =>
        {
            o.OutputFormatters.RemoveType<StringOutputFormatter>();
            foreach (var json in o.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
                json.SupportedMediaTypes.Remove("text/json");
        });
        mvc.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = null;
            AddConverters(o.SerializerOptions);
        });
        return mvc.Services;
    }

    /// <summary>
    /// The converters and the number handling in one place, so the serializers never diverge. A number is
    /// read only from a number: the OpenAPI document then types it integer or number, not "number or
    /// string".
    /// </summary>
    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new NullableDateOnlyJsonConverter());
    }
}
