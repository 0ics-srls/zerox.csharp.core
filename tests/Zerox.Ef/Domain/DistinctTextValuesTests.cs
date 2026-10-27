using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Zerox.Ef.Db;

namespace Zerox.Ef.Domain.Tests;

public sealed class DistinctTextValuesTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        var box = new Box { Label = "b" };
        seed.Boxes.Add(box);
        foreach (var note in new[] { "pear", "apple", "pear", null, "", "fig" })
            seed.Items.Add(new Item { Name = "x", Note = note, Box = box });
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

    [Fact]
    public async Task Should_NotLoadTheRows_WhenDistinctValuesAreAsked()
    {
        var commands = new CommandCapture();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(Db.Connection)
            .UseZeroxSession()
            .AddInterceptors(commands)
            .Options;
        await using var context = new TestDbContext(options);

        await DistinctTextValues.ValuesAsync(context.Items.AsNoTracking(), "Note", Ct);

        var sql = commands.Texts.Should().ContainSingle().Which;
        sql.Should().Contain("DISTINCT").And.NotContain("\"Quantity\"").And.NotContain("\"Name\"");
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
