using System.Globalization;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IRepositoryOfItem = Zerox.Ef.Core.IRepository<Zerox.Ef.Domain.Tests.Item>;
using LoadOptions = Zerox.Ef.Core.LoadOptions;
using OpStatus = Zerox.Ef.Core.OpStatus;

namespace Zerox.Ef.Domain.Tests;

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

    private static IQueryable<Item> Items(TestDbContext context) => context.Items.AsNoTracking();

    private static Task<LoadResult> LoadAsync(TestDbContext context, DataSourceLoadOptionsBase options, int maxTake = Search.DefaultMaxTake) =>
        Search.LoadAsync(Items(context), options, maxTake, Ct);

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

        var result = await LoadAsync(context, options);

        result.data.Cast<Item>().Select(i => i.Quantity).Should().Equal(10, 8, 6, 4, 2);
        result.totalCount.Should().Be(15);
    }

    [Fact]
    public async Task Should_Refuse_WhenRowsOrColumnsAreAskedWithoutTake()
    {
        await using var context = Db.NewContext();

        var rows = () => LoadAsync(context, new DataSourceLoadOptionsBase());
        var columns = () => LoadAsync(context, new DataSourceLoadOptionsBase { Select = ["Name"] });

        (await rows.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("Take");
        (await columns.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("Take");
    }

    [Fact]
    public async Task Should_Refuse_WhenTakeIsAboveTheLimit()
    {
        await using var context = Db.NewContext();

        var aboveDefault = () => LoadAsync(context, new DataSourceLoadOptionsBase { Take = Search.DefaultMaxTake + 1 });
        var aboveOwn = () => LoadAsync(context, new DataSourceLoadOptionsBase { Take = 11 }, maxTake: 10);
        var atOwn = await LoadAsync(context, new DataSourceLoadOptionsBase { Take = 10 }, maxTake: 10);

        await aboveDefault.Should().ThrowAsync<InvalidRequestException>();
        await aboveOwn.Should().ThrowAsync<InvalidRequestException>();
        atOwn.data.Cast<Item>().Should().HaveCount(10);
    }

    [Fact]
    public async Task Should_ReturnRowsAsEntities_WhenNeitherGroupNorSelectIsAsked()
    {
        await using var context = Db.NewContext();

        var result = await LoadAsync(context, new DataSourceLoadOptionsBase { Take = 3 });

        // Materialized: an error of the query leaves the search, not the serializer.
        result.data.Should().BeAssignableTo<System.Collections.IList>();
        result.data.Cast<object>().Should().HaveCount(3).And.AllBeOfType<Item>();
    }

    [Fact]
    public async Task Should_ReturnCountAndSummaryWithoutRows_WhenOnlyCountOrSummaryIsAsked()
    {
        await using var context = Db.NewContext();

        var count = await LoadAsync(context, new DataSourceLoadOptionsBase { IsCountQuery = true });
        var summary = await LoadAsync(context, new DataSourceLoadOptionsBase
        {
            IsSummaryQuery = true,
            TotalSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "sum" }],
        });

        count.data.Cast<object>().Should().BeEmpty();
        count.totalCount.Should().Be(30);
        summary.data.Cast<object>().Should().BeEmpty();
        Convert.ToInt32(summary.summary![0], CultureInfo.InvariantCulture).Should().Be(465);
    }

    [Fact]
    public async Task Should_TurnDevExtremeErrorsIntoInvalidRequest()
    {
        await using var context = Db.NewContext();

        // The validator lets a [NotMapped] property through (it exists); the database cannot sort on it.
        var rows = () => LoadAsync(context, new DataSourceLoadOptionsBase { Sort = [new SortingInfo { Selector = "Display" }], Take = 5 });

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
        var options = new DataSourceLoadOptionsBase { Take = 10 };
        switch (option)
        {
            case "Filter": options.Filter = L("Secret", "=", "s"); break;
            case "Sort": options.Sort = [new SortingInfo { Selector = "Secret" }]; break;
            case "Group": options.Group = [new GroupingInfo { Selector = "Secret" }]; break;
            case "TotalSummary": options.TotalSummary = [new SummaryInfo { Selector = "Secret", SummaryType = "count" }]; break;
            case "Select": options.Select = ["Secret"]; break;
        }

        var search = () => LoadAsync(context, options);

        (await search.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("'Secret' cannot be read here");
    }

    [Fact]
    public async Task Should_AllowANavigation_EvenWhenItIsJsonIgnored()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase { Filter = L("Box.Label", "=", "blue"), RequireTotalCount = true, Take = 100 };

        var result = await LoadAsync(context, options);

        result.totalCount.Should().Be(15);
    }

    [Fact]
    public async Task Should_ReturnKeysCountsAndSumsWithoutRows_WhenGroupIsAsked()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind", IsExpanded = true }],
            GroupSummary = [new SummaryInfo { Selector = "Quantity", SummaryType = "sum" }],
        };

        var result = await LoadAsync(context, options);

        var groups = result.data.Cast<Group>().ToList();
        groups.Should().HaveCount(2);
        var small = groups.Single(g => Equals(g.key, ItemKind.Small));
        small.count.Should().Be(15);
        Convert.ToInt32(small.summary![0], CultureInfo.InvariantCulture).Should().Be(225);
        small.items.Should().BeNull();
    }

    [Fact]
    public async Task Should_NestTheGroups_WhenGroupingOnTwoLevels()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind" }, new GroupingInfo { Selector = "Box.Label" }],
        };

        var result = await LoadAsync(context, options);

        var groups = result.data.Cast<Group>().ToList();
        groups.Should().AllSatisfy(g => g.items.Should().NotBeNull().And.Subject.As<System.Collections.IList>().Cast<object>().Should().HaveCount(2));
        var inner = groups.SelectMany(g => g.items!.Cast<Group>()).ToList();
        inner.Sum(c => c.count ?? 0).Should().Be(30);
        inner.Should().AllSatisfy(c => c.items.Should().BeNull());
    }

    [Fact]
    public async Task Should_GroupByMonth_WhenAnIntervalIsGiven()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase { Group = [new GroupingInfo { Selector = "At", GroupInterval = "month" }] };

        var result = await LoadAsync(context, options);

        result.data.Cast<Group>().Select(g => (Convert.ToInt32(g.key, CultureInfo.InvariantCulture), g.count)).Should().Equal((1, 10), (2, 20));
    }

    [Fact]
    public async Task Should_RefuseSelect_WhenGroupIsAsked()
    {
        await using var context = Db.NewContext();

        var withSelect = () => LoadAsync(context, new DataSourceLoadOptionsBase
        {
            Group = [new GroupingInfo { Selector = "Kind" }],
            Select = ["Name"],
        });

        (await withSelect.Should().ThrowAsync<InvalidRequestException>()).Which.Message.Should().Contain("never rows");
    }

    [Fact]
    public async Task Should_ReturnDictionariesWithCSharpNames_WhenSelectIsAsked()
    {
        await using var context = Db.NewContext();
        var options = new DataSourceLoadOptionsBase
        {
            Select = ["name", "box.label"],
            Sort = [new SortingInfo { Selector = "Quantity" }],
            Take = 1,
        };

        var result = await LoadAsync(context, options);

        var row = result.data.Cast<IDictionary<string, object?>>().Single();
        row.Should().ContainKey("Name").WhoseValue.Should().Be("item01");
        row.Should().ContainKey("Box");
    }

    // --- the library's own options, on any query ---

    // A row of a projection: what a service shows instead of an entity.
    public sealed class ItemLine
    {
        public string Name { get; init; } = string.Empty;

        public int Quantity { get; init; }

        public string BoxLabel { get; init; } = string.Empty;
    }

    private static IQueryable<ItemLine> Lines(TestDbContext context) =>
        context.Items.AsNoTracking().Select(i => new ItemLine { Name = i.Name, Quantity = i.Quantity, BoxLabel = i.Box!.Label });

    [Fact]
    public async Task Should_AnswerTheLoadedRowsAndTotal_WhenAProjectionIsLoadedWithLoadOptions()
    {
        await using var context = Db.NewContext();
        var options = new LoadOptions
        {
            Filter = L("BoxLabel", "=", "blue"),
            Sort = [new() { Selector = "Quantity", Desc = true }],
            Skip = 1,
            Take = 3,
            RequireTotalCount = true,
        };

        var result = await Search.LoadAsync(Lines(context), options, Search.DefaultMaxTake, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Data.Cast<ItemLine>().Select(l => l.Quantity).Should().Equal(29, 28, 27);
        result.Value.TotalCount.Should().Be(15);
    }

    [Fact]
    public async Task Should_AnswerInvalid_WhenTheFilterNamesAnUnknownField()
    {
        await using var context = Db.NewContext();

        var result = await Search.LoadAsync(Lines(context), new LoadOptions { Filter = L("Nope", "=", 1), Take = 5 }, Search.DefaultMaxTake, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("Nope");
    }

    [Fact]
    public async Task Should_AnswerAsEntityServiceDoes_WhenTheSourceIsTheEntities()
    {
        var options = new LoadOptions { Filter = L("Kind", "=", "Large"), Take = 4, RequireTotalCount = true, Sort = [new() { Selector = "Quantity" }] };
        await using var scope = Db.NewScope();
        await using var context = Db.NewContext();

        var viaService = await new EntityService<Item>(scope.ServiceProvider.GetRequiredService<IRepositoryOfItem>()).LoadAsync(options, Ct);
        var viaSearch = await Search.LoadAsync(Items(context), options, Search.DefaultMaxTake, Ct);

        viaSearch.Status.Should().Be(viaService.Status);
        viaSearch.Value!.Data.Cast<Item>().Select(i => i.Quantity).Should().Equal(viaService.Value!.Data.Cast<Item>().Select(i => i.Quantity));
        viaSearch.Value.TotalCount.Should().Be(viaService.Value.TotalCount);
    }
}
