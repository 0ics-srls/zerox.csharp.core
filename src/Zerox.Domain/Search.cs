using System.Collections;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;

namespace Zerox.Domain;

// The DevExtreme search shared by every grid and every client of the load options. A malformed search
// is a 400 with the reason, never a silent 500: every refusal is an InvalidRequestException.
public static class Search
{
    // The options a client does not decide: no synchronous code on the database, grouping and
    // projection done by the provider (with EF, in SQL: false would bring the table into memory).
    public static void MakeSafe(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Skip < 0 || options.Take < 0)
            throw new InvalidRequestException("Skip and Take cannot be negative.");
        options.AllowAsyncOverSync = false;
        options.RemoteGrouping = null;
        options.RemoteSelect = null;
    }

    // Rows: whole entities, paged, filtered, sorted, with the total count and summaries when asked.
    public static async Task<DataSourceResult<T>> RowsAsync<T>(
        IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        MakeSafe(options);
        CheckReserved<T>(options);
        if (options.Group is { Length: > 0 })
            throw new InvalidRequestException("Grouping is not asked here: use the groups route. Here rows are read.");
        // PreSelect alone projects too (measured on 5.1.0): it would return dictionaries, not entities.
        if (options.Select is { Length: > 0 } || options.PreSelect is { Length: > 0 })
            throw new InvalidRequestException("Projection is not asked here: use the projection route. Here whole entities are read.");
        LoadOptionsValidator.Validate<T>(options);

        var loaded = await LoadAsync(source, options, "search", ct).ConfigureAwait(false);
        return new DataSourceResult<T>
        {
            // Materialized here: an error leaves this method, not the serializer. With IsCountQuery and
            // IsSummaryQuery DevExtreme returns no rows (data null, measured on 5.1.0).
            data = loaded.data?.Cast<T>().ToList() ?? [],
            totalCount = loaded.totalCount,
            groupCount = loaded.groupCount,
            summary = loaded.summary,
        };
    }

    // Groups: keys, counts and sums per group, never rows (every level is forced not expanded).
    public static async Task<GroupsResult> GroupsAsync<T>(
        IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        MakeSafe(options);
        if (options.Group is not { Length: > 0 })
            throw new InvalidRequestException("Give at least one field in Group, for example {\"Group\": [{\"Selector\": \"Kind\"}]}.");
        if (options.Select is { Length: > 0 })
            throw new InvalidRequestException("Projection is not asked here: use the projection route. Here groups are counted and summed.");
        CheckReserved<T>(options);
        LoadOptionsValidator.Validate<T>(options);
        foreach (var level in options.Group)
            level.IsExpanded = false;

        var loaded = await LoadAsync(source, options, "grouping", ct).ConfigureAwait(false);
        return new GroupsResult(
            loaded.data.Cast<Group>().Select(ToNode).ToList(),
            loaded.groupCount < 0 ? null : loaded.groupCount,
            loaded.totalCount < 0 ? null : loaded.totalCount,
            loaded.summary);
    }

    // Projection: only the asked columns (also through navigations: names instead of ids), projected in
    // SQL. Keys are the C# names, even when the client wrote them in lower case.
    public static async Task<DataSourceResult<IDictionary<string, object?>>> ProjectionAsync<T>(
        IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        MakeSafe(options);
        // PreSelect alone chooses the columns too (measured on 5.1.0).
        if (options.Select is not { Length: > 0 } && options.PreSelect is not { Length: > 0 })
            throw new InvalidRequestException("Give in Select the fields to read, for example {\"Select\": [\"Name\", \"Box.Label\"]}.");
        if (options.Group is { Length: > 0 })
            throw new InvalidRequestException("Grouping is not asked here: use the groups route. Here columns are read.");
        CheckReserved<T>(options);
        LoadOptionsValidator.Validate<T>(options);
        // DevExtreme uses the received text as key: "box.label" would come out as {"box":{"label":…}},
        // outside the "names as is" rule.
        if (options.Select is { Length: > 0 })
            options.Select = options.Select.Select(CSharpNames<T>).ToArray();
        if (options.PreSelect is { Length: > 0 })
            options.PreSelect = options.PreSelect.Select(CSharpNames<T>).ToArray();

        var loaded = await LoadAsync(source, options, "projection", ct).ConfigureAwait(false);
        return new DataSourceResult<IDictionary<string, object?>>
        {
            data = loaded.data?.Cast<IDictionary<string, object?>>().ToList() ?? [],
            totalCount = loaded.totalCount,
            groupCount = loaded.groupCount,
            summary = loaded.summary,
        };
    }

    // The one place where an exception becomes another: a request DevExtreme or the database cannot
    // run is the client's mistake, a 400 with the reason, not a crash. The original stays as inner.
    private static async Task<LoadResult> LoadAsync<T>(IQueryable<T> source, DataSourceLoadOptionsBase options, string what, CancellationToken ct)
    {
        try
        {
            var loaded = await DataSourceLoader.LoadAsync(source, options, ct).ConfigureAwait(false);
            // Materialize lazy data inside the try: a translation error must surface here.
            if (loaded.data is not null and not IList)
                loaded.data = loaded.data.Cast<object>().ToList();
            return loaded;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            throw new InvalidRequestException($"Invalid {what}: {e.Message}", e);
        }
    }

    // The path with the C# names ("box.label" → "Box.Label"); the validator already proved it exists.
    private static string CSharpNames<T>(string path) =>
        EntityPaths.Resolve(typeof(T), path) is { } chain ? string.Join('.', chain.Select(p => p.Name)) : path;

    private static GroupNode ToNode(Group group) => new(
        group.key,
        group.count,
        group.summary,
        group.items?.OfType<Group>().Select(ToNode).ToList() is { Count: > 0 } children ? children : null);

    // Every path named in the options is resolved segment by segment; a reserved property throws. A
    // route with load options is full access to the table: this keeps a secret out of Select and Group.
    // A shape it does not recognise is skipped and left to the validator.
    private static void CheckReserved<T>(DataSourceLoadOptionsBase options)
    {
        foreach (var path in PathsOf(options))
        {
            var type = typeof(T);
            foreach (var segment in path.Split('.'))
            {
                var property = type.GetProperty(segment, EntityPaths.PublicProperties);
                if (property is null)
                    break;
                if (EntityPaths.IsReserved(property))
                    throw new InvalidRequestException($"The field '{property.Name}' cannot be read here.");
                type = EntityPaths.Inner(property.PropertyType);
            }
        }
    }

    private static IEnumerable<string> PathsOf(DataSourceLoadOptionsBase options)
    {
        foreach (var field in FilterFields(options.Filter))
            yield return field;
        foreach (var s in options.Sort ?? [])
            if (!string.IsNullOrEmpty(s?.Selector))
                yield return s.Selector;
        foreach (var g in options.Group ?? [])
            if (!string.IsNullOrEmpty(g?.Selector))
                yield return g.Selector;
        foreach (var r in (options.TotalSummary ?? []).Concat(options.GroupSummary ?? []))
            if (!string.IsNullOrEmpty(r?.Selector))
                yield return r.Selector;
        foreach (var p in (options.Select ?? []).Concat(options.PreSelect ?? []).Concat(options.PrimaryKey ?? []))
            if (!string.IsNullOrEmpty(p))
                yield return p;
        if (!string.IsNullOrEmpty(options.DefaultSort))
            yield return options.DefaultSort;
    }

    // The fields of a Filter as it comes out of binding: a condition has its field at position 0;
    // ["!", …], groups and lists of conditions are walked. What has another shape is skipped.
    private static IEnumerable<string> FilterFields(IList? filter)
    {
        if (filter is null || filter.Count == 0)
            yield break;
        if (filter[0] is string first && first != "!")
        {
            yield return first;
            yield break;
        }
        foreach (var element in filter)
            if (element is IList child)
                foreach (var field in FilterFields(child))
                    yield return field;
    }
}
