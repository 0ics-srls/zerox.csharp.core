using System.Text.Json.Serialization;

namespace Zerox.Ef.Domain;

#pragma warning disable IDE1006 // lowercase: the DevExtreme contract, the one exception to names as is
#pragma warning disable CA1819 // summary is the array DevExtreme returns, passed through as it is
/// <summary>
/// The result shape DevExtreme clients read. Not a DevExtreme type: it is ours, with the names they expect.
/// </summary>
public sealed class DataSourceResult<T>
{
    /// <summary>
    /// The rows of the page (empty for a count or a summary alone).
    /// </summary>
    [JsonPropertyName("data")]
    public IEnumerable<T> data { get; set; } = [];

    /// <summary>
    /// How many rows match the filter, when RequireTotalCount was asked; -1 otherwise.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int totalCount { get; set; } = -1;

    /// <summary>
    /// How many groups, when RequireGroupCount was asked; -1 otherwise.
    /// </summary>
    [JsonPropertyName("groupCount")]
    public int groupCount { get; set; } = -1;

    /// <summary>
    /// The values of TotalSummary, in the order asked.
    /// </summary>
    [JsonPropertyName("summary")]
    public object[]? summary { get; set; }
}
#pragma warning restore CA1819
#pragma warning restore IDE1006
