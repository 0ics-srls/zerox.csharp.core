using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Zerox.Ef.Core;
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
    public static DataSourceLoadOptionsBase ToDevExtreme(LoadOptions options) => new()
    {
        RequireTotalCount = options.RequireTotalCount,
        RequireGroupCount = options.RequireGroupCount,
        IsCountQuery = options.IsCountQuery,
        IsSummaryQuery = options.IsSummaryQuery,
        Skip = options.Skip,
        Take = options.Take,
        Sort = options.Sort?.Select(s => new DxSorting { Selector = s.Selector, Desc = s.Desc }).ToArray(),
        Group = options.Group?.Select(g => new DxGrouping
        {
            Selector = g.Selector,
            Desc = g.Desc,
            GroupInterval = g.GroupInterval,
            IsExpanded = g.IsExpanded,
        }).ToArray(),
        Filter = options.Filter?.ToList(),
        TotalSummary = options.TotalSummary?.Select(Summary).ToArray(),
        GroupSummary = options.GroupSummary?.Select(Summary).ToArray(),
        Select = options.Select?.ToArray(),
        PreSelect = options.PreSelect?.ToArray(),
        RemoteSelect = options.RemoteSelect,
        RemoteGrouping = options.RemoteGrouping,
        ExpandLinqSumType = options.ExpandLinqSumType,
        PrimaryKey = options.PrimaryKey?.ToArray(),
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
