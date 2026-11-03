using System.Text.Json.Serialization;

namespace Zerox.Ef.Core;

/// <summary>
/// What a load answers, written as DevExtreme's LoadResult (5.1.0): the rows — whole entities or the selected
/// columns — or the groups, the counts asked (-1 when not asked), the summary when asked.
/// </summary>
public sealed class Loaded
{
    /// <summary>
    /// The rows, or the <see cref="LoadGroup"/> of the first level; empty for a count or a summary alone.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<object> Data { get; init; } = [];

    /// <summary>
    /// The rows that match the filter; -1 when not asked.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; } = -1;

    /// <summary>
    /// The top-level groups; -1 when not asked.
    /// </summary>
    [JsonPropertyName("groupCount")]
    public int GroupCount { get; init; } = -1;

    /// <summary>
    /// The total sums, in the order asked; left out when not asked.
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object?>? Summary { get; init; }
}

/// <summary>
/// One group of a load, written as DevExtreme's Group (5.1.0): its key, the inner level's groups (null at the
/// last level: a group never carries rows), how many rows, its sums.
/// </summary>
public sealed class LoadGroup
{
    /// <summary>
    /// The value of the grouping field (or of its interval).
    /// </summary>
    [JsonPropertyName("key")]
    public object? Key { get; init; }

    /// <summary>
    /// The groups of the next level; null at the last level.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<LoadGroup>? Items { get; init; }

    /// <summary>
    /// The rows in the group; left out when unknown.
    /// </summary>
    [JsonPropertyName("count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; init; }

    /// <summary>
    /// The group's sums, in the order asked; left out when not asked.
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object?>? Summary { get; init; }
}
