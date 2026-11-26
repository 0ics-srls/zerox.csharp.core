using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// The distinct values of a text column: what a filter dropdown needs, computed by the database.
/// </summary>
public static class DistinctTextValues
{
    /// <summary>
    /// Whether the name is really a text column of the entity. The name usually comes from a URL: it is
    /// checked against the real properties before anything is built on it. The EXACT name (the names
    /// are the contract); a [NotMapped] property is not a column, and asking it would make the query
    /// fail instead of answering "does not exist"; a reserved field is never listed.
    /// </summary>
    public static bool IsTextColumn<T>(string column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var property = typeof(T).GetProperty(column, BindingFlags.Public | BindingFlags.Instance);
        return property is not null
            && property.PropertyType == typeof(string)
            && property.GetCustomAttribute<NotMappedAttribute>() is null
            && !EntityPaths.IsReserved(property);
    }

    /// <summary>
    /// The distinct values of a text column of the rows the filter keeps, each with the number of those rows that
    /// have it: ordered, without repetitions and without empty values, at most <paramref name="maxTake"/>, computed
    /// by the database. The filter is a load-options filter and goes through the one search (<see cref="Search"/>),
    /// so a file it cannot name — unknown, reserved — is <see cref="OpStatus.Invalid"/> with the reason, as in a
    /// load. The query comes from the caller, so a view of its own (a subset of the rows) is kept.
    /// </summary>
    /// <remarks>
    /// The empty choices are excluded by the filter, not after the take: the take must bound the values —
    /// all of them non-empty — not the groups, whose first in key order are null and "".
    /// </remarks>
    /// <exception cref="ArgumentNullException">The source or the column is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">maxTake is not positive.</exception>
    public static async Task<OpResult<IReadOnlyList<TextValue>>> ValuesAsync<T>(
        IQueryable<T> source, string column, IReadOnlyList<object?>? filter, int maxTake, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTake);

        // The empty choices are excluded by the filter, not after the take: the take must bound the values —
        // all of them non-empty — not the groups, whose first in key order are null and "".
        object[] keepValue = new object[] { column, "<>", "" };
        object?[] keepRow = new object?[] { column, "<>", null };
        IReadOnlyList<object?> onlyValues = filter is null
            ? [keepValue, "and", keepRow]
            : [filter, "and", new object[] { keepValue, "and", keepRow }];

        var options = new LoadOptions { Filter = onlyValues, Group = [new GroupingInfo { Selector = column, IsExpanded = false }], Take = maxTake };

        var loaded = await Search.LoadAsync(source, options, maxTake, ct).ConfigureAwait(false);
        if (loaded.Status != OpStatus.Ok || loaded.Value is null)
            return OpResult.Invalid<IReadOnlyList<TextValue>>(loaded.Reason ?? "The values cannot be read.");

        // One group per distinct value, in the order of the key; no group is empty here: the filter
        // excluded the null and the empty values, so the take bounded the values themselves.
        IReadOnlyList<TextValue> values = [.. loaded.Value.Data.OfType<LoadGroup>()
            .Where(group => group.Key is string { Length: > 0 })
            .Select(group => new TextValue(
                group.Key as string ?? throw new InvalidOperationException("The key of a group of a text column is not text."),
                group.Count ?? throw new InvalidOperationException("A group that is not expanded came without its count.")))];
        return OpResult.Ok(values);
    }
}
