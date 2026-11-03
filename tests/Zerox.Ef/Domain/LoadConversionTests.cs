using System.Collections;
using System.Globalization;
using System.Reflection;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;
using DxGrouping = DevExtreme.AspNet.Data.GroupingInfo;
using DxSorting = DevExtreme.AspNet.Data.SortingInfo;
using DxSummary = DevExtreme.AspNet.Data.SummaryInfo;

namespace Zerox.Ef.Domain.Tests;

// The own load types of Zerox.Ef.Core and DevExtreme's: every member crosses, the groups come back as LoadGroup.
public sealed class LoadConversionTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 30 items in two boxes: Quantity 1..30, Kind Small for odd and Large for even quantities,
    // At in January for the first 10 and in February for the rest; red box 1..15, blue box 16..30.
    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        var red = new Box { Label = "red", Opened = new DateOnly(2026, 1, 1) };
        var blue = new Box { Label = "blue", Opened = new DateOnly(2026, 2, 1) };
        seed.Boxes.AddRange(red, blue);
        for (var q = 1; q <= 30; q++)
        {
            seed.Items.Add(new Item
            {
                Name = $"item{q:00}",
                Quantity = q,
                Kind = q % 2 == 1 ? ItemKind.Small : ItemKind.Large,
                At = new DateTime(2026, q <= 10 ? 1 : 2, 1 + (q % 20), 0, 0, 0, DateTimeKind.Utc),
                Box = q <= 15 ? red : blue,
                Secret = "s",
            });
        }
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<(LoadResult Raw, Loaded Converted)> LoadAsync(LoadOptions options)
    {
        await using var context = Db.NewContext();
        var raw = await Search.LoadAsync(context.Items.AsNoTracking(), LoadConversion.ToDevExtreme(options), Search.DefaultMaxTake, Ct);
        return (raw, LoadConversion.ToLoaded(raw));
    }

    private static IEnumerable<string> InstanceProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

    // Same value, member by member: scalars by equality, sequences element by element, objects property by
    // property of the DevExtreme side.
    private static void ShouldMatch(object? ours, object? theirs, string path)
    {
        if (ours is null || theirs is null)
        {
            (ours is null).Should().Be(theirs is null, path);
            return;
        }
        if (theirs is string || theirs.GetType().IsValueType)
        {
            theirs.Should().Be(ours, path);
            return;
        }
        if (theirs is IEnumerable sequence)
        {
            var left = ((IEnumerable)ours).Cast<object?>().ToList();
            var right = sequence.Cast<object?>().ToList();
            right.Should().HaveCount(left.Count, path);
            for (var i = 0; i < left.Count; i++)
                ShouldMatch(left[i], right[i], $"{path}[{i}]");
            return;
        }
        foreach (var property in theirs.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            ShouldMatch(ours.GetType().GetProperty(property.Name)!.GetValue(ours), property.GetValue(theirs), $"{path}.{property.Name}");
    }

    [Fact]
    public void Should_HaveEveryMemberOfDevExtreme_WhenLoadOptionsIsInspected()
    {
        (Type Ours, Type Theirs)[] pairs =
        [
            (typeof(LoadOptions), typeof(DataSourceLoadOptionsBase)),
            (typeof(Core.SortingInfo), typeof(DxSorting)),
            (typeof(Core.GroupingInfo), typeof(DxGrouping)),
            (typeof(Core.SummaryInfo), typeof(DxSummary)),
        ];

        var missing = pairs.SelectMany(p => InstanceProperties(p.Theirs).Except(InstanceProperties(p.Ours)).Select(name => $"{p.Ours.Name}.{name}"));

        missing.Should().BeEmpty();
    }

    [Fact]
    public void Should_CopyEveryMember_WhenConvertedToDevExtreme()
    {
        var options = new LoadOptions
        {
            RequireTotalCount = true,
            RequireGroupCount = true,
            IsCountQuery = true,
            IsSummaryQuery = true,
            Skip = 3,
            Take = 7,
            Sort = [new() { Selector = "Quantity", Desc = true }],
            Group = [new() { Selector = "At", Desc = true, GroupInterval = "month", IsExpanded = true }],
            Filter = ["Kind", "=", "Large"],
            TotalSummary = [new() { Selector = "Quantity", SummaryType = "sum" }],
            GroupSummary = [new() { Selector = "Price", SummaryType = "max" }],
            Select = ["Name"],
            PreSelect = ["Name", "Quantity"],
            RemoteSelect = true,
            RemoteGrouping = true,
            ExpandLinqSumType = true,
            PrimaryKey = ["Id"],
            DefaultSort = "Name",
            StringToLower = true,
            PaginateViaPrimaryKey = true,
            SortByPrimaryKey = true,
            AllowAsyncOverSync = true,
        };

        var converted = LoadConversion.ToDevExtreme(options);

        ShouldMatch(options, converted, nameof(LoadOptions));
    }

    [Fact]
    public void Should_LeaveTheCollectionsNull_WhenNotGiven()
    {
        var converted = LoadConversion.ToDevExtreme(new LoadOptions());

        new object?[]
        {
            converted.Sort, converted.Group, converted.Filter, converted.TotalSummary, converted.GroupSummary,
            converted.Select, converted.PreSelect, converted.PrimaryKey,
        }.Should().AllSatisfy(collection => collection.Should().BeNull());
    }

    [Fact]
    public async Task Should_KeepTheRows_WhenConvertedBack()
    {
        var (raw, converted) = await LoadAsync(new LoadOptions { Take = 5, Sort = [new() { Selector = "Quantity" }] });

        converted.Data.Should().Equal(raw.data.Cast<object>(), ReferenceEquals);
        converted.Data.Cast<Item>().Select(i => i.Quantity).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Should_TurnGroupsIntoLoadGroups_WhenConvertedBack()
    {
        var (_, converted) = await LoadAsync(new LoadOptions
        {
            Group = [new() { Selector = "Kind" }],
            GroupSummary = [new() { Selector = "Quantity", SummaryType = "sum" }],
        });

        var groups = converted.Data.Should().AllBeOfType<LoadGroup>().Subject.ToList();
        groups.Should().HaveCount(2);
        var small = groups.Single(g => Equals(g.Key, ItemKind.Small));
        small.Count.Should().Be(15);
        Convert.ToInt32(small.Summary!.Single(), CultureInfo.InvariantCulture).Should().Be(225);
        small.Items.Should().BeNull();
    }

    [Fact]
    public async Task Should_NestTheGroups_WhenConvertedBack()
    {
        var (_, converted) = await LoadAsync(new LoadOptions
        {
            Group = [new() { Selector = "Kind" }, new() { Selector = "Box.Label" }],
        });

        var groups = converted.Data.Cast<LoadGroup>().ToList();
        groups.Should().AllSatisfy(g => g.Items.Should().HaveCount(2));
        groups.SelectMany(g => g.Items!).Sum(inner => inner.Count ?? 0).Should().Be(30);
    }

    [Fact]
    public async Task Should_CopyCountsAndSummary_WhenConvertedBack()
    {
        var (_, converted) = await LoadAsync(new LoadOptions
        {
            IsSummaryQuery = true,
            RequireTotalCount = true,
            TotalSummary = [new() { Selector = "Quantity", SummaryType = "sum" }],
        });

        converted.Data.Should().BeEmpty();
        converted.TotalCount.Should().Be(30);
        Convert.ToInt32(converted.Summary!.Single(), CultureInfo.InvariantCulture).Should().Be(465);
    }
}
