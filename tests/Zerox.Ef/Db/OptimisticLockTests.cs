using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

public sealed class OptimisticLockTests : IAsyncLifetime
{
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

    [Fact]
    public async Task Should_MarkVersionAsConcurrencyToken_WhenTheEntityImplementsTheInterface()
    {
        await using var context = Db.NewContext();

        context.Model.FindEntityType(typeof(Locked))!.FindProperty(nameof(Locked.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task Should_NotMarkAnything_WhenTheEntityDoesNotImplementIt()
    {
        await using var context = Db.NewContext();

        context.Model.FindEntityType(typeof(Item))!.GetProperties().Should().NotContain(p => p.IsConcurrencyToken);
    }

    [Fact]
    public async Task Should_IncrementVersion_WhenAModifiedEntityIsSaved()
    {
        var id = await SeedAsync("a");

        await using (var scope = Db.NewScope())
        {
            var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
            var row = await rows.GetByIdAsync(id, Ct);
            row!.Name = "b";
            await rows.SaveChangesAsync(Ct);
        }

        (await ReadAsync(id)).Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_KeepVersionZero_WhenAnEntityIsAdded()
    {
        var id = await SeedAsync("a");

        (await ReadAsync(id)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Should_StartAtZero_WhenAnAddedEntityCarriesAVersion()
    {
        int id;
        await using (var context = Db.NewContext())
        {
            var row = new Locked { Name = "a", Version = 7 };
            context.Locked.Add(row);
            await context.SaveChangesAsync(Ct);
            id = row.Id;
            row.Version.Should().Be(0);
        }

        (await ReadAsync(id)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReachTwo_WhenModifiedTwice()
    {
        var id = await SeedAsync("a");

        await using (var scope = Db.NewScope())
        {
            var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
            var row = await rows.GetByIdAsync(id, Ct);
            row!.Name = "b";
            await rows.SaveChangesAsync(Ct);
            row.Name = "c";
            await rows.SaveChangesAsync(Ct);
        }

        (await ReadAsync(id)).Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_NotIncrement_WhenTheSaveIsSwallowedByABoundary()
    {
        var id = await SeedAsync("a");

        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        await using var work = rows.OpenUnitOfWork();
        var row = await rows.GetByIdAsync(id, Ct);
        row!.Name = "b";

        await rows.SaveChangesAsync(Ct);
        row.Version.Should().Be(0);

        await work.ApplyAsync(Ct);
        (await ReadAsync(id)).Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_ThrowConcurrency_WhenTheClientVersionIsStale()
    {
        var id = await SeedAsync("a");

        await using var scope = Db.NewScope();
        var rows = scope.ServiceProvider.GetRequiredService<IRepository<Locked>>();
        await RenameElsewhereAsync(id, "other");   // the row is at Version 1 now

        var row = await rows.GetByIdAsync(id, Ct);
        rows.PinReadVersion(row!, 0);              // the client read it at 0
        row!.Name = "mine";
        var save = () => rows.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
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
        a!.Name = "a2";
        b!.Name = "b2";
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
        (await rows.GetByIdAsync(id, Ct))!.Name.Should().Be("a");   // a tracked copy, soon stale
        await RenameElsewhereAsync(id, "fresh");

        rows.DetachAll();

        (await rows.GetByIdAsync(id, Ct))!.Name.Should().Be("fresh");
    }
}
