using System.Collections;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// The DevExtreme search shared by every grid and every client of the load options. A malformed search
/// is a 400 with the reason, never a silent 500: every refusal is an InvalidRequestException.
/// </summary>
public static class Search
{
    /// <summary>
    /// The most rows one request reads when the caller does not set its own limit.
    /// </summary>
    public const int DefaultMaxTake = 1000;

    /// <summary>
    /// The options a client does not decide: no synchronous code on the database, grouping and
    /// projection done by the provider (with EF, in SQL: false would bring the table into memory).
    /// </summary>
    public static void MakeSafe(DataSourceLoadOptionsBase options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Skip < 0 || options.Take < 0)
            throw new InvalidRequestException("Skip and Take cannot be negative.");
        options.AllowAsyncOverSync = false;
        options.RemoteGrouping = null;
        options.RemoteSelect = null;
    }

    /// <summary>
    /// The one search: the request decides what comes back, in DevExtreme's own result, as it is. Without
    /// Group and Select, whole entities; with Select, only the asked columns (also through navigations:
    /// names instead of ids), projected in SQL, with the C# names even when the client wrote them in lower
    /// case; with Group, keys, counts and sums per group, never rows (every level is forced not expanded).
    /// Rows and columns are paged within the limit; a count or a summary alone returns no rows.
    /// </summary>
    public static async Task<LoadResult> LoadAsync<T>(
        IQueryable<T> source, DataSourceLoadOptionsBase options, int maxTake = DefaultMaxTake, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        MakeSafe(options);
        CheckReserved<T>(options);
        LoadOptionsValidator.Validate<T>(options);
        if (options.Group is { Length: > 0 })
        {
            // PreSelect alone projects too (measured on 5.1.0).
            if (options.Select is { Length: > 0 } || options.PreSelect is { Length: > 0 })
                throw new InvalidRequestException("Select has no effect with Group: a group carries its key, count and sums, never rows.");
            foreach (var level in options.Group)
                level.IsExpanded = false;
        }
        else
        {
            CheckPage(options, maxTake);
            // DevExtreme uses the received text as key: "box.label" would come out as {"box":{"label":…}},
            // outside the "names as is" rule.
            if (options.Select is { Length: > 0 })
                options.Select = options.Select.Select(CSharpNames<T>).ToArray();
            if (options.PreSelect is { Length: > 0 })
                options.PreSelect = options.PreSelect.Select(CSharpNames<T>).ToArray();
        }

        var loaded = await LoadCoreAsync(source, options, ct).ConfigureAwait(false);
        // With IsCountQuery and IsSummaryQuery DevExtreme returns no rows (data null, measured on 5.1.0):
        // the client always reads a list.
        loaded.data ??= Array.Empty<object>();
        return loaded;
    }

    /// <summary>
    /// The one search on any query, from the library's own load options: the rows, groups or total as
    /// <see cref="Loaded"/> in a result. An option the query cannot answer (unknown field, operator, value type,
    /// a page above <paramref name="maxTake"/>) is <see cref="OpStatus.Invalid"/> with the reason, never an
    /// exception; any other failure is left to the caller.
    /// </summary>
    public static async Task<OpResult<Loaded>> LoadAsync<T>(
        IQueryable<T> source, LoadOptions options, int maxTake = DefaultMaxTake, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            return OpResult.Ok(LoadConversion.ToLoaded(
                await LoadAsync(source, LoadConversion.ToDevExtreme(options), maxTake, ct).ConfigureAwait(false)));
        }
        catch (InvalidRequestException invalid)
        {
            return OpResult.Invalid<Loaded>(invalid.Message);
        }
    }

    // The one place where an exception becomes another: a request DevExtreme or the database cannot
    // run is the client's mistake, a 400 with the reason, not a crash. The original stays as inner.
    private static async Task<LoadResult> LoadCoreAsync<T>(IQueryable<T> source, DataSourceLoadOptionsBase options, CancellationToken ct)
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
            throw new InvalidRequestException($"Invalid search: {e.Message}", e);
        }
    }

    // A request that returns rows says how many, within the limit: without Take DevExtreme reads the whole
    // table. A count or a summary alone returns no rows and needs no page.
    private static void CheckPage(DataSourceLoadOptionsBase options, int maxTake)
    {
        if (options.IsCountQuery == true || options.IsSummaryQuery == true)
            return;
        if (options.Take <= 0)
            throw new InvalidRequestException($"Give Take, the rows to read (at most {maxTake}): {{\"Skip\": 0, \"Take\": 50}}.");
        if (options.Take > maxTake)
            throw new InvalidRequestException($"Take is at most {maxTake}: read the rest with Skip.");
    }

    // The path with the C# names ("box.label" → "Box.Label"); the validator already proved it exists.
    private static string CSharpNames<T>(string path) =>
        EntityPaths.Resolve(typeof(T), path) is { } chain ? string.Join('.', chain.Select(p => p.Name)) : path;

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
