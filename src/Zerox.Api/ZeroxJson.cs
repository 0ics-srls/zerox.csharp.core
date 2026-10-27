using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Api;

// The JSON "as is": the names of the entities travel exactly as they are written (no camelCase), enums
// as their names, days as yyyy-MM-dd — the same in controllers, minimal APIs and OpenAPI.
public static class ZeroxJson
{
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

    // The converters in one place, so the serializers never diverge.
    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new NullableDateOnlyJsonConverter());
    }
}
