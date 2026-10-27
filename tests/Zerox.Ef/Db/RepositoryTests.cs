using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

public sealed class RepositoryTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<int> SeedAsync(params Item[] items)
    {
        await using var context = Db.NewContext();
        context.Items.AddRange(items);
        await context.SaveChangesAsync(Ct);
        return items[0].Id;
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenTheDatabaseIsNew()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        (await items.Query().ToListAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_AddTheEntity_WhenAddedAndSaved()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await items.AddAsync(new Item { Name = "pen", Quantity = 3 }, Ct);
            await items.SaveChangesAsync(Ct);
        }

        await using var check = Db.NewContext();
        (await check.Items.SingleAsync(Ct)).Name.Should().Be("pen");
    }

    [Fact]
    public async Task Should_ReturnTheEntity_WhenGetByIdFindsIt()
    {
        var id = await SeedAsync(new Item { Name = "pen", Quantity = 1 });

        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            var item = await items.GetByIdAsync(id, Ct);
            item.Should().NotBeNull();
            item!.Quantity = 5;   // tracked: the change is saved without Update
            await items.SaveChangesAsync(Ct);
        }

        await using var check = Db.NewContext();
        (await check.Items.SingleAsync(Ct)).Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenGetByIdFindsNothing()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        (await items.GetByIdAsync(404, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Should_PersistTheChange_WhenUpdatedAndSaved()
    {
        var id = await SeedAsync(new Item { Name = "pen", Quantity = 1 });

        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            items.Update(new Item { Id = id, Name = "pencil", Quantity = 2 });
            await items.SaveChangesAsync(Ct);
        }

        await using var check = Db.NewContext();
        var saved = await check.Items.SingleAsync(Ct);
        saved.Name.Should().Be("pencil");
        saved.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Should_RemoveTheEntity_WhenDeletedAndSaved()
    {
        var id = await SeedAsync(new Item { Name = "a" }, new Item { Name = "b" }, new Item { Name = "c" });

        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            items.Delete((await items.GetByIdAsync(id, Ct))!);
            await items.SaveChangesAsync(Ct);
        }

        await using (var check = Db.NewContext())
            (await check.Items.Select(i => i.Name).OrderBy(n => n).ToListAsync(Ct)).Should().Equal("b", "c");

        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            items.DeleteRange(await items.Query(tracked: true).ToListAsync(Ct));
            await items.SaveChangesAsync(Ct);
        }

        await using var empty = Db.NewContext();
        (await empty.Items.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Should_NotTrack_WhenQueriedByDefault()
    {
        await SeedAsync(new Item { Name = "pen" });

        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        var read = await items.Query().SingleAsync(Ct);
        context.Entry(read).State.Should().Be(EntityState.Detached);

        var tracked = await items.Query(tracked: true).SingleAsync(Ct);
        context.Entry(tracked).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task Should_SupportLinq_WhenFilteringAndOrdering()
    {
        await SeedAsync(
            new Item { Name = "c", Quantity = 3 },
            new Item { Name = "a", Quantity = 1 },
            new Item { Name = "b", Quantity = 2 },
            new Item { Name = "z", Quantity = 9 });

        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var query = items.Query().Where(i => i.Quantity < 5).OrderBy(i => i.Name);

        query.ToQueryString().Should().Contain("WHERE").And.Contain("ORDER BY");
        (await query.Select(i => i.Name).ToListAsync(Ct)).Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task Should_ReturnTheNumberOfChanges_WhenSaved()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await items.AddAsync(new Item { Name = "a" }, Ct);
        await items.AddAsync(new Item { Name = "b" }, Ct);

        (await items.SaveChangesAsync(Ct)).Should().Be(2);
    }

    [Fact]
    public async Task Should_SeeUnsavedRows_WhenAskingLocal()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var pen = new Item { Name = "pen" };
        await items.AddAsync(pen, Ct);

        items.Local().Should().Contain(pen);
        (await items.Query().AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_Throw_WhenSavingSynchronously()
    {
        await using (var context = Db.NewContext())
        {
            context.Items.Add(new Item { Name = "pen" });
            // The synchronous save is exactly what this test proves refused.
            // ast-grep-ignore: no-sync-dbcontext-savechanges
            var act = () => context.SaveChanges();
            act.Should().Throw<SyncDatabaseAccessException>();
        }

        await using var check = Db.NewContext();
        (await check.Items.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_RefuseTheContext_WhenUseZeroxSessionIsMissing()
    {
        // The real way it happens: the context is registered by hand with options of its own before
        // AddZeroxDb (AddDbContext adds the context with TryAdd, so the hand-made one wins). Two
        // AddDbContext calls do NOT lose the session: since EF Core 9 their configurations add up.
        var withoutSession = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(Db.Connection).Options;
        await using var provider = new ServiceCollection()
            .AddScoped(_ => new TestDbContext(withoutSession))
            .AddZeroxDb<TestDbContext>(o => o.UseSqlite(Db.Connection))
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRepository<Item>>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*UseZeroxSession*");
    }

    [Fact]
    public async Task Should_RegisterThePortsButNotTheBoundary_WhenAddZeroxDbIsCalled()
    {
        await using var scope = Db.NewScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<IDbContext>().Should().BeSameAs(services.GetRequiredService<TestDbContext>());
        services.GetService<IRepository<Item>>().Should().NotBeNull();
        services.GetService<IRepositoryFactory>().Should().NotBeNull();

        // The boundary and the massive handler are handed out by a repository, never injected.
        new ServiceCollection().AddZeroxDb<TestDbContext>(o => o.UseSqlite(Db.Connection))
            .Should().NotContain(d => d.ServiceType.Name.Contains("UnitOfWork") || d.ServiceType.Name.Contains("Massive"));
    }

    [Fact]
    public async Task Should_ShareTheContext_WhenTheFactoryMakesARepository()
    {
        await using var scope = Db.NewScope();
        var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
        var items = scope.ServiceProvider.GetRequiredService<IRepositoryFactory>().For<Item>();

        var pen = new Item { Name = "pen" };
        await items.AddAsync(pen, Ct);
        var box = new Box { Label = "b1" };
        await boxes.AddAsync(box, Ct);

        boxes.Local().Should().Contain(box);
        scope.ServiceProvider.GetRequiredService<TestDbContext>().Items.Local.Should().Contain(pen);
    }
}
