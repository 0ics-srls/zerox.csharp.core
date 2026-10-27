using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Ef.Api;

/// <summary>
/// The JSON "as is": the names of the entities travel exactly as they are written (no camelCase), enums
/// as their names, days as yyyy-MM-dd — the same in controllers, minimal APIs and OpenAPI.
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
        mvc.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = null;
            AddConverters(o.SerializerOptions);
        });
        return mvc.Services;
    }

    /// <summary>
    /// The converters in one place, so the serializers never diverge.
    /// </summary>
    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new NullableDateOnlyJsonConverter());
    }
}
