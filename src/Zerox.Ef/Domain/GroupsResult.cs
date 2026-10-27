namespace Zerox.Ef.Domain;

// The groups of a search: keys, counts and sums, never rows.
// GroupCount and TotalCount are there when asked (RequireGroupCount, RequireTotalCount).
public sealed record GroupsResult(IReadOnlyList<GroupNode> Groups, int? GroupCount, int? TotalCount, IReadOnlyList<object?>? Summary);

// A group: its key, how many rows, the GroupSummary values, and the groups of the next level.
public sealed record GroupNode(object? Key, int? Count, IReadOnlyList<object?>? Summary, IReadOnlyList<GroupNode>? Children);
