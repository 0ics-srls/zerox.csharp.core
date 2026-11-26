using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Zerox.Ef.Core;
using Zerox.Ef.Db;

namespace Zerox.Ef.Domain.Tests;

public sealed class DistinctTextValuesTests : IAsyncLifetime
{
    // ["Quantity", "<=", 2]: the rows pear(1) and apple(2) of the seed.
    private static readonly IReadOnlyList<object?> FirstTwo = ["Quantity", "<=", 2];

    private const int Limit = 100;

    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        var box = new Box { Label = "b" };
        seed.Boxes.Add(box);
        var notes = new[] { "pear", "apple", "pear", null, "", "fig" };
        for (var i = 0; i < notes.Length; i++)
            seed.Items.Add(new Item { Name = "x", Note = notes[i], Quantity = i + 1, Box = box });
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    [Fact]
    public async Task Should_ReturnDistinctOrderedValuesWithoutEmpty()
    {
        await using var context = Db.NewContext();

        var values = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", Ct);

        values.Should().Equal("apple", "fig", "pear");
    }

    [Fact]
    public async Task Should_ReturnDistinctOrderedValuesWithoutEmpty_WhenThereIsNoFilter()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", null, Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Select(v => v.Value).Should().Equal("apple", "fig", "pear");
    }

    [Fact]
    public async Task Should_CountTheRowsOfEachValue_WhenThereIsNoFilter()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", null, Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(new TextValue("apple", 1), new TextValue("fig", 1), new TextValue("pear", 2));
    }

    [Fact]
    public async Task Should_CountOnlyTheFilteredRows_WhenAFilterIsGiven()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", FirstTwo, Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().Equal(new TextValue("apple", 1), new TextValue("pear", 1));
    }

    [Fact]
    public async Task Should_ReturnOnlyTheValuesOfTheFilteredRows_WhenAFilterIsGiven()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", FirstTwo, Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Select(v => v.Value).Should().Equal("apple", "pear");
    }

    [Fact]
    public async Task Should_ReturnTheSameValuesAsTheLoadGroupedOnTheColumn_WhenAFilterIsGiven()
    {
        await using var context = Db.NewContext();
        var grouped = await Search.LoadAsync(context.Items.AsNoTracking(),
            new LoadOptions { Filter = FirstTwo, Group = [new GroupingInfo { Selector = "Note", IsExpanded = false }], Take = Limit }, Limit, Ct);
        var keys = grouped.Value?.Data.OfType<LoadGroup>().Where(g => g.Key is string { Length: > 0 })
            .Select(g => (g.Key, g.Count));

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", FirstTwo, Limit, Ct);

        result.Value!.Select(v => ((object?)v.Value, (int?)v.Count)).Should().Equal(keys);
    }

    [Fact]
    public async Task Should_ReturnNoValues_WhenTheFilterMatchesNoRow()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", ["Quantity", ">", 100], Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_KeepTheViewOfTheQuery_WhenTheSourceIsNarrowed()
    {
        await using var context = Db.NewContext();
        var narrowed = context.Items.AsNoTracking().Where(i => i.Quantity >= 5);

        var result = await DistinctTextValues.ValuesAsync(narrowed, "Note", null, Limit, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Select(v => v.Value).Should().Equal("fig");
    }

    [Fact]
    public async Task Should_ReturnAtMostMaxTakeValues_WhenThereAreMore()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", null, 2, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value!.Select(v => v.Value).Should().Equal("apple", "fig");
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheFilterNamesAnUnknownField()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", ["Nope", "=", 1], Limit, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("Nope");
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenTheFilterNamesAReservedField()
    {
        await using var context = Db.NewContext();

        var result = await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", ["Secret", "=", "s"], Limit, Ct);

        result.Status.Should().Be(OpStatus.Invalid);
        result.Reason.Should().Contain("Secret");
    }

    [Fact]
    public async Task Should_NotLoadTheRows_WhenDistinctValuesAreAsked()
    {
        var commands = new CommandCapture();
        var options = Db.Use(new DbContextOptionsBuilder<TestDbContext>())
            .UseZeroxSession()
            .AddInterceptors(commands)
            .Options;
        await using var context = new TestDbContext(options);

        await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", null, Limit, Ct);

        var sql = commands.Texts.Should().ContainSingle().Which;
        sql.Should().Contain("\"Note\"").And.NotContain("\"Quantity\"").And.NotContain("\"Name\"");
    }

    [Fact]
    public async Task Should_ThrowArgumentNull_WhenTheSourceOrTheColumnIsNull()
    {
        await using var context = Db.NewContext();
        var source = context.Items.AsNoTracking();

        var nullSource = async () => await DistinctTextValues.ValuesAsync<Item>(null!, "Note", null, Limit, Ct);
        var nullColumn = async () => await DistinctTextValues.ValuesAsync(source, null!, null, Limit, Ct);

        await nullSource.Should().ThrowAsync<ArgumentNullException>();
        await nullColumn.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_ThrowArgumentOutOfRange_WhenMaxTakeIsNotPositive()
    {
        await using var context = Db.NewContext();
        var source = context.Items.AsNoTracking();

        var zeroTake = async () => await DistinctTextValues.ValuesAsync(source, "Note", null, 0, Ct);

        await zeroTake.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_RecognizeOnlyExactMappedTextColumns()
    {
        DistinctTextValues.IsTextColumn<Item>("Name").Should().BeTrue();
        DistinctTextValues.IsTextColumn<Item>("name").Should().BeFalse();
        DistinctTextValues.IsTextColumn<Item>("Quantity").Should().BeFalse();
        DistinctTextValues.IsTextColumn<Item>("Display").Should().BeFalse();
        DistinctTextValues.IsTextColumn<Item>("Missing").Should().BeFalse();
    }

    [Fact]
    public void Should_RefuseAReservedColumn_WhenItIsText()
    {
        DistinctTextValues.IsTextColumn<Item>("Secret").Should().BeFalse();
    }

    private sealed class CommandCapture : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
