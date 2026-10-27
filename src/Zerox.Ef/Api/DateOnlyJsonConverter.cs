using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zerox.Ef.Api;

/// <summary>
/// A day travels as yyyy-MM-dd.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    /// <inheritdoc/>
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateOnly.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// A day that may be missing: null, or an empty string, is no day.
/// </summary>
public sealed class NullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    /// <inheritdoc/>
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}
