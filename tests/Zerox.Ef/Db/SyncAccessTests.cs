using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

// The session refuses every synchronous road to the database: what a repository or a context does
// against the database is async, or it does not happen.
public sealed class SyncAccessTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        seed.Items.Add(new Item { Name = "pen" });
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    [Fact]
    public async Task Should_Refuse_WhenARepositoryQueryIsReadSynchronously()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var act = () => items.Query().Where(i => i.Name == "pen").ToList();

        act.Should().Throw<SyncDatabaseAccessException>();
    }

    [Fact]
    public async Task Should_Refuse_WhenACountIsAskedSynchronously()
    {
        await using var context = Db.NewContext();

        var act = () => context.Items.Count();

        act.Should().Throw<SyncDatabaseAccessException>();
    }

    [Fact]
    public async Task Should_Refuse_WhenACommandRunsSynchronously()
    {
        await using var context = Db.NewContext();

        var act = () => context.Database.ExecuteSqlRaw("DELETE FROM Items");

        act.Should().Throw<SyncDatabaseAccessException>();
        (await context.Items.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task Should_Refuse_WhenATransactionStartsSynchronously()
    {
        await using var context = Db.NewContext();

        var act = () => context.Database.BeginTransaction();

        act.Should().Throw<SyncDatabaseAccessException>();
    }

    [Fact]
    public async Task Should_Refuse_WhenAConnectionOpensSynchronously()
    {
        await using var own = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(own).UseZeroxSession().Options);

        var act = () => context.Database.OpenConnection();

        act.Should().Throw<SyncDatabaseAccessException>();
    }

    [Fact]
    public async Task Should_ReadAndWrite_WhenEverythingIsAsync()
    {
        await using var context = Db.NewContext();

        context.Items.Add(new Item { Name = "ink" });
        await context.SaveChangesAsync(Ct);

        (await context.Items.CountAsync(Ct)).Should().Be(2);
    }
}
