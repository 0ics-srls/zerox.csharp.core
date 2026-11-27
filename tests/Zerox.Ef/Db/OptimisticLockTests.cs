using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Core;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Db.Tests;

// The row version is the database's: EF reads back what it generated, nothing of ours writes it. The lock
// is proved on PostgreSQL; SQLite has no row version, and says so.
public sealed class OptimisticLockTests : IAsyncLifetime
{
    private const string PostgresOnly = "SQLite has no row version: the lock is proved on PostgreSQL.";
    private const string SqliteOnly = "SQLite only.";
    private const string Category = "Zerox.Ef.Db";

    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<int> SeedAsync(string name)
    {
        await using var seed = Db.NewContext();
        var row = new Locked { Name = name };
        seed.Locked.Add(row);
        await seed.SaveChangesAsync(Ct);
        return row.Id;
    }

    private async Task<Locked> ReadAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Locked.SingleAsync(l => l.Id == id, Ct);
    }

    private async Task RenameElsewhereAsync(int id, string name)
    {
        await using var other = Db.NewContext();
        var row = await other.Locked.SingleAsync(l => l.Id == id, Ct);
        row.Name = name;
        await other.SaveChangesAsync(Ct);
    }

    // A context whose model is built here, with this logger factory (EF builds a model once per type).
    private RowVersionDbContext WatchedContext(ILoggerFactory logs) =>
        new(Db.Use(new DbContextOptionsBuilder<RowVersionDbContext>()).UseLoggerFactory(logs).Options);

    // The tracked row is renamed, another writer saves in between, then the tracked row is saved.
    private async Task SaveAfterAnotherWriterAsync(int id)
    {
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        var row = await rows.GetByIdAsync(id, Ct);
        row.Should().NotBeNull();
        await RenameElsewhereAsync(id, "other");
        row.Name = "mine";
        await rows.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Should_MapTheRowVersionToXmin_WhenOnPostgres()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        await using var context = Db.NewContext();

        var locked = context.Model.FindEntityType(typeof(Locked));
        locked.Should().NotBeNull();
        var version = locked.FindProperty(nameof(Locked.Version));
        version.Should().NotBeNull();

        version.GetColumnName().Should().Be("xmin");
        version.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        version.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReadBackTheVersionTheDatabaseGave_WhenARowIsAdded()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        await using var context = Db.NewContext();
        var row = new Locked { Name = "a" };
        context.Locked.Add(row);

        await context.SaveChangesAsync(Ct);

        row.Version.Should().NotBe(0u);
        row.Version.Should().Be((await ReadAsync(row.Id)).Version);
    }

    [Fact]
    public async Task Should_ChangeTheVersion_WhenAModifiedRowIsSaved()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var before = (await ReadAsync(id)).Version;
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        var row = await rows.GetByIdAsync(id, Ct);
        row.Should().NotBeNull();
        row.Name = "b";

        await rows.SaveChangesAsync(Ct);

        var stored = (await ReadAsync(id)).Version;
        stored.Should().NotBe(before);
        row.Version.Should().Be(stored);
    }

    [Fact]
    public async Task Should_ChangeTheVersion_WhenAMassiveUpdateWritesTheRow()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var before = (await ReadAsync(id)).Version;
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();

        await using (var massive = await rows.OpenMassiveAsync(Ct))
        {
            await massive.UpdateWhereAsync(l => l.Id == id, f => f.Set(l => l.Name, "massive"), Ct);
            await massive.CommitAsync(Ct);
        }

        (await ReadAsync(id)).Version.Should().NotBe(before);
    }

    [Fact]
    public async Task Should_ChangeTheVersion_WhenSqlWritesTheRow()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var before = (await ReadAsync(id)).Version;
        await using var context = Db.NewContext();

        await context.Database.ExecuteSqlRawAsync("""UPDATE "Locked" SET "Name" = 'sql' WHERE "Id" = {0}""", [id], Ct);

        (await ReadAsync(id)).Version.Should().NotBe(before);
    }

    [Fact]
    public async Task Should_ChangeTheStoredVersionOnlyOnApply_WhenSavedInsideAUnitOfWork()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var before = (await ReadAsync(id)).Version;
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        await using var work = rows.OpenUnitOfWork();
        var row = await rows.GetByIdAsync(id, Ct);
        row.Should().NotBeNull();
        row.Name = "b";
        await rows.SaveChangesAsync(Ct);
        (await ReadAsync(id)).Version.Should().Be(before);

        await work.ApplyAsync(Ct);

        (await ReadAsync(id)).Version.Should().NotBe(before);
    }

    [Fact]
    public async Task Should_ThrowConcurrency_WhenAnotherWriterSavedBetweenTheReadAndTheSave()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");

        var save = () => SaveAfterAnotherWriterAsync(id);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await ReadAsync(id)).Name.Should().Be("other");
    }

    [Fact]
    public async Task Should_RefuseARowVersionThatIsNotAUint_WhenOnPostgres()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        await using var context = new WideDbContext(Db.Use(new DbContextOptionsBuilder<WideDbContext>()).Options);

        var build = () => context.Model;

        build.Should().Throw<NotSupportedException>().WithMessage("*Wide.Version*uint*");
    }

    [Fact]
    public async Task Should_StoreZeroAndKeepIt_WhenOnSqlite()
    {
        Assert.SkipWhen(TestDatabase.OnPostgres, SqliteOnly);
        var id = await SeedAsync("a");
        (await ReadAsync(id)).Version.Should().Be(0u);

        await RenameElsewhereAsync(id, "b");

        (await ReadAsync(id)).Version.Should().Be(0u);
    }

    [Fact]
    public async Task Should_SaveARowReadBeforeAnotherWrite_WhenOnSqlite()
    {
        Assert.SkipWhen(TestDatabase.OnPostgres, SqliteOnly);
        var id = await SeedAsync("a");

        await SaveAfterAnotherWriterAsync(id);

        (await ReadAsync(id)).Name.Should().Be("mine");
    }

    [Fact]
    public async Task Should_WarnOnceThatTheLockIsOff_WhenTheModelHasARowVersionOnSqlite()
    {
        Assert.SkipWhen(TestDatabase.OnPostgres, SqliteOnly);
        var capture = new LogCapture();
        using var logs = LoggerFactory.Create(b => b.AddProvider(capture));

        await using (var first = WatchedContext(logs))
            _ = first.Model;
        await using (var second = WatchedContext(logs))
            _ = second.Model;

        var warning = capture.Entries.Where(e => e.Category == Category).Should().ContainSingle().Subject;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().Contain(nameof(Locked));
    }

    [Fact]
    public async Task Should_NotWarn_WhenOnPostgres()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var capture = new LogCapture();
        using var logs = LoggerFactory.Create(b => b.AddProvider(capture));
        await using var context = WatchedContext(logs);

        _ = context.Model;

        capture.Entries.Should().NotContain(e => e.Category == Category);
    }

    [Fact]
    public async Task Should_RefuseARowVersion_WhenItIsNotANumber()
    {
        await using var context = new BlobDbContext(Db.Use(new DbContextOptionsBuilder<BlobDbContext>()).Options);

        var build = () => context.Model;

        build.Should().Throw<NotSupportedException>().WithMessage("*Blob.Version*");
    }

    [Fact]
    public async Task Should_NotMarkAnything_WhenTheEntityHasNoRowVersion()
    {
        await using var context = Db.NewContext();

        var item = context.Model.FindEntityType(typeof(Item));
        item.Should().NotBeNull();
        item.GetProperties().Should().NotContain(p => p.IsConcurrencyToken);
    }

    [Fact]
    public async Task Should_DiscardEveryTrackedRowOfTheType_WhenDetachAll()
    {
        var first = await SeedAsync("a");
        var second = await SeedAsync("b");
        await using (var seed = Db.NewContext())
        {
            seed.Items.Add(new Item { Name = "pen" });
            await seed.SaveChangesAsync(Ct);
        }

        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var a = await rows.GetByIdAsync(first, Ct);
        var b = await rows.GetByIdAsync(second, Ct);
        a.Should().NotBeNull();
        b.Should().NotBeNull();
        a.Name = "a2";
        b.Name = "b2";
        var pen = await items.Query(tracked: true).SingleAsync(Ct);

        rows.DetachAll();

        context.Entry(a).State.Should().Be(EntityState.Detached);
        context.Entry(b).State.Should().Be(EntityState.Detached);
        context.Entry(pen).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task Should_ReadTheDatabase_WhenReReadAfterDetachAll()
    {
        var id = await SeedAsync("a");

        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        var stale = await rows.GetByIdAsync(id, Ct);   // a tracked copy, soon stale
        stale.Should().NotBeNull();
        stale.Name.Should().Be("a");
        await RenameElsewhereAsync(id, "fresh");

        rows.DetachAll();

        var fresh = await rows.GetByIdAsync(id, Ct);
        fresh.Should().NotBeNull();
        fresh.Name.Should().Be("fresh");
    }
}
