using System.Text.Json;
using DevExtreme.AspNet.Data;
using Microsoft.EntityFrameworkCore;

namespace Zerox.Domain.Tests;

public sealed class SearchTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static List<object?> L(params object?[] elements) => [.. elements];

    // 30 items in two boxes: Quantity 1..30, Kind Small for odd and Large for even quantities,
    // At in January for the first 10 and in February for the rest.
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

    private static readonly JsonSerializerOptions AsIs = new() { PropertyNamingPolicy = null };

    private static IQueryable<Item> Items(TestDbContext context) => context.Items.AsNoTracking();

    [Fact]
    public void Should_ReturnTheDevExtremeShape_WhenSerialized()
    {
        var json = JsonSerializer.Serialize(new DataSourceResult<Item>(), AsIs);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("data", "totalCount", "groupCount", "summary");
    }

    [Fact]
    public async Task Should_PageFilterAndSort_WhenRowsAreAsked()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Filter = L("Kind", "=", "Large"),
            Sort = [new SortingInfo { Selector = "Quantity", Desc = true }],
            Skip = 10,
            Take = 5,
            RequireTotalCount = true,
        };

        var result = await Search.RowsAsync(Items(context), options, Ct);

        result.data.Select(i => i.Quantity).Should().Equal(10, 8, 6, 4, 2);
        result.totalCount.Should().Be(15);
    }

    [Fact]
    public async Task Should_ReturnRowsAsEntities_WhenRowsAreAsked()
    {
        await using var context = Db.NewContext();

        var result = await Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { Take = 3 }, Ct);

        result.data.Should().BeOfType<List<Item>>().Which.Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_ReturnCountAndSummaryWithoutRows_WhenOnlyCountOrSummaryIsAsked()
    {
        await using var context = Db.NewContext();

        var count = await Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { IsCountQuery = true }, Ct);
        var summary = await Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase
        {
            IsSummaryQuery = true,
            TotalSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "sum" }],
        }, Ct);

        count.data.Should().NotBeNull().And.BeEmpty();
        count.totalCount.Should().Be(30);
        summary.data.Should().NotBeNull().And.BeEmpty();
        Convert.ToInt32(summary.summary![0], System.Globalization.CultureInfo.InvariantCulture).Should().Be(465);
    }

    [Fact]
    public async Task Should_PointToTheGroupsRoute_WhenGroupIsAskedForRows()
    {
        await using var context = Db.NewContext();

        var rows = () => Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { Group = [new GroupingInfo { Selector = "Kind" }] }, Ct);

        (await rows.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("groups route");
    }

    [Fact]
    public async Task Should_PointToTheProjectionRoute_WhenSelectOrPreSelectIsAskedForRows()
    {
        await using var context = Db.NewContext();

        var select = () => Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { Select = ["Name"] }, Ct);
        var preSelect = () => Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { PreSelect = ["Name"] }, Ct);

        (await select.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("projection route");
        (await preSelect.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("projection route");
    }

    [Fact]
    public async Task Should_TurnDevExtremeErrorsIntoInvalidRequest()
    {
        await using var context = Db.NewContext();

        // The validator lets a [NotMapped] property through (it exists); the database cannot sort on it.
        var rows = () => Search.RowsAsync(Items(context), new DataSourceLoadOptionsBase { Sort = [new SortingInfo { Selector = "Display" }] }, Ct);

        (await rows.Should().ThrowAsync<InvalidRequestException>()).Which.InnerException.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Filter")]
    [InlineData("Sort")]
    [InlineData("Group")]
    [InlineData("TotalSummary")]
    [InlineData("Select")]
    public async Task Should_RefuseAReservedField_InFilterSortGroupSummaryAndSelect(string option)
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase();
        switch (option)
        {
            case "Filter": options.Filter = L("Secret", "=", "s"); break;
            case "Sort": options.Sort = [new SortingInfo { Selector = "Secret" }]; break;
            case "Group": options.Group = [new GroupingInfo { Selector = "Secret" }]; break;
            case "TotalSummary": options.TotalSummary = [new SummaryInfo { Selector = "Secret", SummaryType = "count" }]; break;
            case "Select": options.Select = ["Secret"]; break;
        }

        Func<Task> search = option switch
        {
            "Group" => () => Search.GroupsAsync(Items(context), options, Ct),
            "Select" => () => Search.ProjectionAsync(Items(context), options, Ct),
            _ => () => Search.RowsAsync(Items(context), options, Ct),
        };

        (await search.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("'Secret' cannot be read here");
    }

    [Fact]
    public async Task Should_AllowANavigation_EvenWhenItIsJsonIgnored()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase { Filter = L("Box.Label", "=", "blue"), RequireTotalCount = true };

        var result = await Search.RowsAsync(Items(context), options, Ct);

        result.totalCount.Should().Be(15);
    }

    [Fact]
    public async Task Should_ReturnKeysCountsAndSums_WhenGroupsAreAsked()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind" }],
            GroupSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "sum" }],
        };

        var result = await Search.GroupsAsync(Items(context), options, Ct);

        result.Groups.Should().HaveCount(2);
        var small = result.Groups.Single(g => Equals(g.Key, ItemKind.Small));
        small.Count.Should().Be(15);
        Convert.ToInt32(small.Summary![0], System.Globalization.CultureInfo.InvariantCulture).Should().Be(225);
        small.Children.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnChildren_WhenGroupingOnTwoLevels()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind" }, new GroupingInfo { Selector = "Box.Label" }],
        };

        var result = await Search.GroupsAsync(Items(context), options, Ct);

        result.Groups.Should().AllSatisfy(g => g.Children.Should().NotBeNull().And.HaveCount(2));
        result.Groups.SelectMany(g => g.Children!).Sum(c => c.Count ?? 0).Should().Be(30);
    }

    [Fact]
    public async Task Should_GroupByMonth_WhenAnIntervalIsGiven()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase { Group = [new GroupingInfo { Selector = "At", GroupInterval = "month" }] };

        var result = await Search.GroupsAsync(Items(context), options, Ct);

        result.Groups.Select(g => (Convert.ToInt32(g.Key, System.Globalization.CultureInfo.InvariantCulture), g.Count)).Should().Equal((1, 10), (2, 20));
    }

    [Fact]
    public async Task Should_RequireGroup_WhenGroupsAreAsked()
    {
        await using var context = Db.NewContext();

        var noGroup = () => Search.GroupsAsync(Items(context), new DataSourceLoadOptionsBase(), Ct);
        var withSelect = () => Search.GroupsAsync(Items(context), new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind" }],
            Select = ["Name"],
        }, Ct);

        await noGroup.Should().ThrowAsync<InvalidRequestException>();
        (await withSelect.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("projection route");
    }

    [Fact]
    public async Task Should_ReturnDictionariesWithCSharpNames_WhenProjectionIsAsked()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Select = ["name", "box.label"],
            Sort = [new SortingInfo { Selector = "Quantity" }],
            Take = 1,
        };

        var result = await Search.ProjectionAsync(Items(context), options, Ct);

        var row = result.data.Single();
        row.Should().ContainKey("Name").WhoseValue.Should().Be("item01");
        row.Should().ContainKey("Box");
    }

    [Fact]
    public async Task Should_RequireSelect_WhenProjectionIsAsked()
    {
        await using var context = Db.NewContext();

        var noSelect = () => Search.ProjectionAsync(Items(context), new DataSourceLoadOptionsBase(), Ct);
        var withGroup = () => Search.ProjectionAsync(Items(context), new DataSourceLoadOptionsBase
        {
            Select = ["Name"],
            Group = [new GroupingInfo { Selector = "Kind" }],
        }, Ct);

        await noSelect.Should().ThrowAsync<InvalidRequestException>();
        (await withGroup.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("groups route");
    }
}
