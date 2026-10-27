using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Zerox.Domain;

// The distinct values of a text column: what a filter dropdown needs, computed by the database.
public static class DistinctTextValues
{
    // Whether the name is really a text column of the entity. The name usually comes from a URL: it is
    // checked against the real properties before anything is built on it. The EXACT name (the names
    // are the contract); a [NotMapped] property is not a column, and asking it would make the query
    // fail instead of answering "does not exist".
    public static bool IsTextColumn<T>(string column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var property = typeof(T).GetProperty(column, BindingFlags.Public | BindingFlags.Instance);
        return property is not null
            && property.PropertyType == typeof(string)
            && property.GetCustomAttribute<NotMappedAttribute>() is null;
    }

    // Ordered, without repetitions and without empty values (an empty choice would filter on "" and
    // empty the list without saying why). The query comes from the caller, so a view of its own
    // (a subset of the rows) is kept.
    public static async Task<IReadOnlyList<string>> ValuesAsync<T>(IQueryable<T> query, string column, CancellationToken ct = default)
        where T : class =>
        await query
            .Select(row => EF.Property<string>(row, column))
            .Where(value => value != null && value != "")
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
