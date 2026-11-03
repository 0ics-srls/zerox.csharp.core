using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zerox.Ef.Core;

// A text a client may send as a JSON number too, read as its digits: a group interval is "month" or 10, as
// DevExtreme reads it (its own converter is internal, and Core references no package).
internal sealed class NumberOrStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
        JsonTokenType.Null => null,
        _ => throw new JsonException($"A text or a number was expected, not {reader.TokenType}."),
    };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
