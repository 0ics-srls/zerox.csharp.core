using System.Collections;
using System.Text.Json;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Zerox.Ef.Core;
using CoreGrouping = Zerox.Ef.Core.GroupingInfo;
using CoreSorting = Zerox.Ef.Core.SortingInfo;
using CoreSummary = Zerox.Ef.Core.SummaryInfo;
using DxGroup = DevExtreme.AspNet.Data.ResponseModel.Group;
using DxGrouping = DevExtreme.AspNet.Data.GroupingInfo;
using DxSorting = DevExtreme.AspNet.Data.SortingInfo;
using DxSummary = DevExtreme.AspNet.Data.SummaryInfo;

namespace Zerox.Ef.Domain;

// The own load types of Zerox.Ef.Core and DevExtreme's: the search runs on DevExtreme, the contracts never name
// it. Every member crosses by name; what the server decides is still overridden by Search.MakeSafe.
internal static class LoadConversion
{
    // A null entry of a list says nothing: null is "not set", so it is left out.
    public static DataSourceLoadOptionsBase ToDevExtreme(LoadOptions options) => new()
    {
        RequireTotalCount = options.RequireTotalCount,
        RequireGroupCount = options.RequireGroupCount,
        IsCountQuery = options.IsCountQuery,
        IsSummaryQuery = options.IsSummaryQuery,
        Skip = options.Skip,
        Take = options.Take,
        Sort = options.Sort?.OfType<CoreSorting>().Select(s => new DxSorting { Selector = s.Selector, Desc = s.Desc }).ToArray(),
        Group = options.Group?.OfType<CoreGrouping>().Select(g => new DxGrouping
        {
            Selector = g.Selector,
            Desc = g.Desc,
            GroupInterval = g.GroupInterval,
            IsExpanded = g.IsExpanded,
        }).ToArray(),
        Filter = FilterOf(options.Filter),
        TotalSummary = options.TotalSummary?.OfType<CoreSummary>().Select(Summary).ToArray(),
        GroupSummary = options.GroupSummary?.OfType<CoreSummary>().Select(Summary).ToArray(),
        Select = options.Select?.OfType<string>().ToArray(),
        PreSelect = options.PreSelect?.OfType<string>().ToArray(),
        RemoteSelect = options.RemoteSelect,
        RemoteGrouping = options.RemoteGrouping,
        ExpandLinqSumType = options.ExpandLinqSumType,
        PrimaryKey = options.PrimaryKey?.OfType<string>().ToArray(),
        DefaultSort = options.DefaultSort,
        StringToLower = options.StringToLower,
        PaginateViaPrimaryKey = options.PaginateViaPrimaryKey,
        SortByPrimaryKey = options.SortByPrimaryKey,
        AllowAsyncOverSync = options.AllowAsyncOverSync,
    };

    // The rows stay as DevExtreme gave them (entities or the selected columns); a group becomes a LoadGroup at
    // every level.
    public static Loaded ToLoaded(LoadResult result) => new()
    {
        Data = (result.data?.Cast<object>() ?? []).Select(Row).ToList(),
        TotalCount = result.totalCount,
        GroupCount = result.groupCount,
        Summary = result.summary,
    };

    // A filter that came from JSON holds JsonElements; DevExtreme reads its filter with its own converter (internal),
    // so such a filter goes through DevExtreme's own deserialization — the values it gets are the ones it always got.
    // A filter built in the process is copied as it is. A JSON part may sit at any depth (a caller combines the
    // client's filter with conditions of its own), so the walk is recursive.
    private static IList? FilterOf(IReadOnlyList<object?>? filter) =>
        filter switch
        {
            null => null,
            _ when HasJsonElement(filter) =>
                JsonSerializer.Deserialize<DataSourceLoadOptionsBase>(JsonSerializer.SerializeToUtf8Bytes(new { Filter = filter }))!.Filter,
            _ => filter.ToList(),
        };

    // The filter of the values (a caller combined the client's with conditions of its own) as the load binds it.
    internal static IReadOnlyList<object?>? BoundFilter(IReadOnlyList<object?>? filter) =>
        FilterOf(filter) is IList list ? [.. list.Cast<object?>()] : null;

    private static bool HasJsonElement(IReadOnlyList<object?> filter) =>
        filter.Any(element => element is JsonElement
            || (element is IReadOnlyList<object?> nested && HasJsonElement(nested)));

    private static DxSummary Summary(CoreSummary summary) => new() { Selector = summary.Selector, SummaryType = summary.SummaryType };

    private static object Row(object row) => row is DxGroup group ? Group(group) : row;

    private static LoadGroup Group(DxGroup group) => new()
    {
        Key = group.key,
        Items = group.items?.Cast<DxGroup>().Select(Group).ToList(),
        Count = group.count,
        Summary = group.summary,
    };
}
