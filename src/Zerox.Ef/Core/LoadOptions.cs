namespace Zerox.Ef.Core;

/// <summary>
/// The options of one load, member by member those of DevExtreme's DataSourceLoadOptionsBase (5.1.0), with the
/// same names and meaning: the JSON a client sends binds unchanged, and the contracts name no DevExtreme type.
/// The server still decides what a client does not (no synchronous road, grouping and projection by the
/// provider, the page limit): the members it forces are overridden before the load runs.
/// </summary>
public sealed class LoadOptions
{
    /// <summary>
    /// Count the rows that match the filter (totalCount).
    /// </summary>
    public bool RequireTotalCount { get; set; }

    /// <summary>
    /// Count the top-level groups (groupCount).
    /// </summary>
    public bool RequireGroupCount { get; set; }

    /// <summary>
    /// Only the count: no rows.
    /// </summary>
    public bool IsCountQuery { get; set; }

    /// <summary>
    /// Only the summary: no rows.
    /// </summary>
    public bool IsSummaryQuery { get; set; }

    /// <summary>
    /// The rows to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// The rows to read.
    /// </summary>
    public int Take { get; set; }

    /// <summary>
    /// The order of the rows.
    /// </summary>
    public IReadOnlyList<SortingInfo>? Sort { get; set; }

    /// <summary>
    /// The grouping levels: keys, counts and sums per group, never rows.
    /// </summary>
    public IReadOnlyList<GroupingInfo>? Group { get; set; }

    /// <summary>
    /// DevExtreme's filter in its array form: <c>["Field", "op", value]</c>, <c>["!", filter]</c>, filters joined by
    /// "and" / "or", nested arrays inside.
    /// </summary>
    public IReadOnlyList<object?>? Filter { get; set; }

    /// <summary>
    /// The sums over every row that matches the filter.
    /// </summary>
    public IReadOnlyList<SummaryInfo>? TotalSummary { get; set; }

    /// <summary>
    /// The sums of each group.
    /// </summary>
    public IReadOnlyList<SummaryInfo>? GroupSummary { get; set; }

    /// <summary>
    /// Only these fields of each row (names through navigations with a dot).
    /// </summary>
    public IReadOnlyList<string>? Select { get; set; }

    /// <summary>
    /// The fields projected before the rest of the load runs.
    /// </summary>
    public IReadOnlyList<string>? PreSelect { get; set; }

    /// <summary>
    /// Project in the query (the server forces its own choice).
    /// </summary>
    public bool? RemoteSelect { get; set; }

    /// <summary>
    /// Group in the query (the server forces its own choice).
    /// </summary>
    public bool? RemoteGrouping { get; set; }

    /// <summary>
    /// Widen the type of a sum to avoid an overflow.
    /// </summary>
    public bool? ExpandLinqSumType { get; set; }

    /// <summary>
    /// The key fields, for a stable order of the pages.
    /// </summary>
    public IReadOnlyList<string>? PrimaryKey { get; set; }

    /// <summary>
    /// The field that orders the rows when no sort is given.
    /// </summary>
    public string? DefaultSort { get; set; }

    /// <summary>
    /// Compare strings in lower case.
    /// </summary>
    public bool? StringToLower { get; set; }

    /// <summary>
    /// Page through the key instead of skipping rows.
    /// </summary>
    public bool? PaginateViaPrimaryKey { get; set; }

    /// <summary>
    /// Add the key to the order of the rows.
    /// </summary>
    public bool? SortByPrimaryKey { get; set; }

    /// <summary>
    /// Allow a synchronous road inside the load (the server always refuses it).
    /// </summary>
    public bool AllowAsyncOverSync { get; set; }
}

/// <summary>
/// One level of the order of the rows.
/// </summary>
public sealed class SortingInfo
{
    /// <summary>
    /// The field (a navigation's with a dot).
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// Descending.
    /// </summary>
    public bool Desc { get; set; }
}

/// <summary>
/// One grouping level.
/// </summary>
public sealed class GroupingInfo
{
    /// <summary>
    /// The field (a navigation's with a dot).
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// Descending keys.
    /// </summary>
    public bool Desc { get; set; }

    /// <summary>
    /// A date part ("year", "quarter", "month", "day", "dayOfWeek", "hour", "minute", "second") or a number's width.
    /// </summary>
    public string? GroupInterval { get; set; }

    /// <summary>
    /// Whether the group carries its inner level (the server never lets it carry rows).
    /// </summary>
    public bool? IsExpanded { get; set; }
}

/// <summary>
/// One sum, total or per group.
/// </summary>
public sealed class SummaryInfo
{
    /// <summary>
    /// The field.
    /// </summary>
    public string? Selector { get; set; }

    /// <summary>
    /// "sum", "min", "max", "avg" or "count".
    /// </summary>
    public string? SummaryType { get; set; }
}
