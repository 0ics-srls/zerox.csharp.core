using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Core.Data;

namespace Zerox.Db.Tests;

public sealed class MassiveOpTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await TestDb.CreateAsync();
        await using var seed = Db.NewContext();
        seed.Items.AddRange(
            new Item { Name = "a", Quantity = 1 },
            new Item { Name = "b", Quantity = 2 },
            new Item { Name = "c", Quantity = 3 },
            new Item { Name = "d", Quantity = 10 });
        await seed.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<List<(string Name, int Quantity)>> RowsAsync()
    {
        await using var check = Db.NewContext();
        return (await check.Items.OrderBy(i => i.Name).ToListAsync(Ct)).Select(i => (i.Name, i.Quantity)).ToList();
    }

    [Fact]
    public async Task Should_NotOpen_WhenThereArePendingChanges()
    {
        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var pending = new Item { Name = "pending" };
        await items.AddAsync(pending, Ct);

        var open = () => items.OpenMassiveAsync(Ct);

        await open.Should().ThrowAsync<InvalidOperationException>();
        context.Entry(pending).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task Should_OpenAndClearTheTracker_WhenTheTrackerHoldsOnlyReadRows()
    {
        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await items.Query(tracked: true).FirstAsync(Ct);
        context.ChangeTracker.Entries().Should().NotBeEmpty();

        await using var massive = await items.OpenMassiveAsync(Ct);

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowOnSave_WhileOpen()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
        await using var massive = await items.OpenMassiveAsync(Ct);

        await boxes.AddAsync(new Box { Label = "b1" }, Ct);
        var save = () => boxes.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_Throw_WhenASecondMassiveOpIsOpened()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
        await using var massive = await items.OpenMassiveAsync(Ct);

        var second = () => boxes.OpenMassiveAsync(Ct);

        await second.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_NotOpen_InsideABoundary()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await using var work = items.OpenUnitOfWork();

        var open = () => items.OpenMassiveAsync(Ct);

        await open.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_UpdateOnlyTheFilteredRows_WhenUpdateWhere()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using var massive = await items.OpenMassiveAsync(Ct);

            var touched = await massive.UpdateWhereAsync(
                i => i.Quantity < 3,
                fields => fields.Set(i => i.Quantity, 0).Set(i => i.Name, "zero"),
                Ct);

            touched.Should().Be(2);
            await massive.CommitAsync(Ct);
        }

        (await RowsAsync()).Should().Equal(("c", 3), ("d", 10), ("zero", 0), ("zero", 0));
    }

    [Fact]
    public async Task Should_DeleteOnlyTheFilteredRows_WhenDeleteWhere()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using var massive = await items.OpenMassiveAsync(Ct);

            (await massive.DeleteWhereAsync(i => i.Quantity >= 3, Ct)).Should().Be(2);
            await massive.CommitAsync(Ct);
        }

        (await RowsAsync()).Should().Equal(("a", 1), ("b", 2));
    }

    [Fact]
    public async Task Should_RollBack_WhenDisposedWithoutCommit()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using (var massive = await items.OpenMassiveAsync(Ct))
                await massive.DeleteWhereAsync(_ => true, Ct);

            (await RowsAsync()).Should().HaveCount(4);

            await items.AddAsync(new Item { Name = "e" }, Ct);
            (await items.SaveChangesAsync(Ct)).Should().Be(1);
        }

        (await RowsAsync()).Should().HaveCount(5);
    }

    [Fact]
    public async Task Should_Persist_WhenCommitted()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using (var massive = await items.OpenMassiveAsync(Ct))
            {
                await massive.UpdateWhereAsync(i => i.Name == "a", fields => fields.Set(i => i.Quantity, 99), Ct);
                await massive.CommitAsync(Ct);
            }

            (await RowsAsync()).Should().Contain(("a", 99));

            await items.AddAsync(new Item { Name = "e" }, Ct);
            (await items.SaveChangesAsync(Ct)).Should().Be(1);
        }

        (await RowsAsync()).Should().HaveCount(5);
    }
}
