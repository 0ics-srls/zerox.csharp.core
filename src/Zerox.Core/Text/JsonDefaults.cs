using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zerox.Core.Text;

// The JSON setup of the whole library: names as in C#, enums as member names.
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string ToJson<T>(this T value) => JsonSerializer.Serialize(value, Options);

    public static T? FromJson<T>(this string json) => JsonSerializer.Deserialize<T>(json, Options);

    public static Task ToJsonAsync<T>(this Stream stream, T value, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);

    public static ValueTask<T?> FromJsonAsync<T>(this Stream stream, CancellationToken cancellationToken = default)
        => JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
