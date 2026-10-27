using System.Text.Json.Serialization;

namespace Zerox.Domain;

// The result shape DevExtreme clients read. Not a DevExtreme type: it is ours, with the names they expect.
#pragma warning disable IDE1006 // lowercase: the DevExtreme contract, the one exception to names as is
#pragma warning disable CA1819 // summary is the array DevExtreme returns, passed through as it is
public sealed class DataSourceResult<T>
{
    [JsonPropertyName("data")]
    public IEnumerable<T> data { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int totalCount { get; set; } = -1;

    [JsonPropertyName("groupCount")]
    public int groupCount { get; set; } = -1;

    [JsonPropertyName("summary")]
    public object[]? summary { get; set; }
}
#pragma warning restore CA1819
#pragma warning restore IDE1006
