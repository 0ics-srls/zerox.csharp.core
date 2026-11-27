using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Db.Tests;

// A row as the client sent it, saved or deleted at the version the client read, without reading it first:
// the database refuses a row changed since. The lock is proved on PostgreSQL; SQLite has none.
public sealed class ClientRowTests : IAsyncLifetime
{
    private const string PostgresOnly = "SQLite has no row version: the lock is proved on PostgreSQL.";
    private const string SqliteOnly = "SQLite only.";
    private const int Missing = 999;

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

    private async Task<int> SeedItemAsync(string name, int quantity)
    {
        await using var seed = Db.NewContext();
        var item = new Item { Name = name, Quantity = quantity };
        seed.Items.Add(item);
        await seed.SaveChangesAsync(Ct);
        return item.Id;
    }

    private async Task<Locked?> ReadAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Locked.SingleOrDefaultAsync(l => l.Id == id, Ct);
    }

    // A row the test knows is there: a missing one fails the test here, saying so.
    private async Task<Locked> StoredAsync(int id)
    {
        var row = await ReadAsync(id);
        row.Should().NotBeNull("row {0} is there", id);
        return row;
    }

    private async Task<Item?> ReadItemAsync(int id)
    {
        await using var check = Db.NewContext();
        return await check.Items.SingleOrDefaultAsync(i => i.Id == id, Ct);
    }

    private async Task RenameElsewhereAsync(int id, string name)
    {
        await using var other = Db.NewContext();
        var row = await other.Locked.SingleAsync(l => l.Id == id, Ct);
        row.Name = name;
        await other.SaveChangesAsync(Ct);
    }

    private async Task MassiveRenameAsync(int id, string name)
    {
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        await using var massive = await rows.OpenMassiveAsync(Ct);
        await massive.UpdateWhereAsync(l => l.Id == id, f => f.Set(l => l.Name, name), Ct);
        await massive.CommitAsync(Ct);
    }

    // The client's row saved in a scope of its own, at the version it carries.
    private async Task SaveClientRowAsync(Locked row, params string[] fields)
    {
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        rows.Update(row, fields);
        await rows.SaveChangesAsync(Ct);
    }

    private async Task DeleteAsync<T>(int id, ulong? version) where T : class, IEntity
    {
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<T>>();
        rows.TryDeleteById(id, version).Should().BeTrue();
        await rows.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Should_WriteOnlyTheNamedFields_WhenARowIsUpdatedAsTheClientSentIt()
    {
        var id = await SeedItemAsync("pen", quantity: 3);
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        items.Update(new Item { Id = id, Name = "pencil", Quantity = 99 }, [nameof(Item.Name)]);

        await items.SaveChangesAsync(Ct);

        var stored = await ReadItemAsync(id);
        stored.Should().NotBeNull();
        stored.Name.Should().Be("pencil");
        stored.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Should_NeverWriteTheKeyOrTheRowVersion_WhenTheyAreNamed()
    {
        var id = await SeedAsync("a");
        var stored = (await StoredAsync(id)).Version;

        await SaveClientRowAsync(new Locked { Id = id, Name = "b", Version = stored }, nameof(Locked.Id), nameof(Locked.Name), nameof(Locked.Version));

        (await StoredAsync(id)).Name.Should().Be("b");
    }

    [Fact]
    public async Task Should_RefuseAField_WhenTheEntityDoesNotHaveIt()
    {
        var id = await SeedItemAsync("pen", quantity: 3);
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var update = () => items.Update(new Item { Id = id }, ["Nope"]);

        update.Should().Throw<ArgumentException>().WithMessage("*Nope*");
    }

    [Fact]
    public async Task Should_ThrowConcurrency_WhenTheUpdatedRowDoesNotExist()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        items.Update(new Item { Id = Missing, Name = "x" }, [nameof(Item.Name)]);

        var save = () => items.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Should_DeleteTheRow_WhenDeletedByIdWithoutARowVersion()
    {
        var id = await SeedItemAsync("pen", quantity: 3);

        await DeleteAsync<Item>(id, null);

        (await ReadItemAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowConcurrency_WhenTheDeletedRowDoesNotExist()
    {
        var delete = () => DeleteAsync<Item>(Missing, null);

        await delete.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Should_RefuseADeleteWithoutTheVersion_WhenTheEntityHasARowVersion()
    {
        var id = await SeedAsync("a");
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();

        var delete = () => rows.TryDeleteById(id, null);

        delete.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Should_DeleteNothing_WhenTheVersionDoesNotFitTheRowVersion()
    {
        var id = await SeedAsync("a");
        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();

        var marked = rows.TryDeleteById(id, ulong.MaxValue);

        marked.Should().BeFalse();
        await rows.SaveChangesAsync(Ct);
        (await ReadAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Should_ReportARowVersion_WhenTheEntityHasOne()
    {
        await using var scope = Db.NewScope();

        var locked = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>().HasRowVersion;
        var item = scope.ServiceProvider.GetRequiredService<IRepository<Item>>().HasRowVersion;

        locked.Should().BeTrue();
        item.Should().BeFalse();
    }

    [Fact]
    public async Task Should_SaveTheClientRow_WhenItCarriesTheCurrentVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;
        var row = new Locked { Id = id, Name = "mine", Version = read };

        await SaveClientRowAsync(row, nameof(Locked.Name));

        var stored = await StoredAsync(id);
        stored.Name.Should().Be("mine");
        stored.Version.Should().NotBe(read);
        row.Version.Should().Be(stored.Version);
    }

    [Fact]
    public async Task Should_ThrowConcurrencyAndKeepTheRow_WhenTheClientRowCarriesAStaleVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;
        await RenameElsewhereAsync(id, "other");

        var save = () => SaveClientRowAsync(new Locked { Id = id, Name = "mine", Version = read }, nameof(Locked.Name));

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await StoredAsync(id)).Name.Should().Be("other");
    }

    [Fact]
    public async Task Should_ThrowConcurrency_WhenTheVersionWasReadBeforeAMassiveUpdate()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;   // the client's GET
        await MassiveRenameAsync(id, "massive");      // a write outside SaveChanges

        var save = () => SaveClientRowAsync(new Locked { Id = id, Name = "mine", Version = read }, nameof(Locked.Name));

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await StoredAsync(id)).Name.Should().Be("massive");
    }

    [Fact]
    public async Task Should_DeleteTheRow_WhenDeletedByIdAtTheCurrentVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;

        await DeleteAsync<Locked>(id, read);

        (await ReadAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowConcurrencyAndKeepTheRow_WhenDeletedByIdAtAStaleVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;
        await RenameElsewhereAsync(id, "other");

        var delete = () => DeleteAsync<Locked>(id, read);

        await delete.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await ReadAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Should_SaveAStaleClientRow_WhenOnSqlite()
    {
        Assert.SkipWhen(TestDatabase.OnPostgres, SqliteOnly);
        var id = await SeedAsync("a");
        var read = (await StoredAsync(id)).Version;
        await RenameElsewhereAsync(id, "other");

        await SaveClientRowAsync(new Locked { Id = id, Name = "mine", Version = read }, nameof(Locked.Name));

        (await StoredAsync(id)).Name.Should().Be("mine");
    }
}
