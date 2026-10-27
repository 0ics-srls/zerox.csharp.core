using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

public sealed class UnitOfWorkTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private async Task<(int Items, int Boxes)> CountAsync()
    {
        await using var check = Db.NewContext();
        return (await check.Items.CountAsync(Ct), await check.Boxes.CountAsync(Ct));
    }

    [Fact]
    public async Task Should_Write_WhenNoBoundaryIsOpen()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await items.AddAsync(new Item { Name = "pen" }, Ct);
            (await items.SaveChangesAsync(Ct)).Should().Be(1);
        }

        (await CountAsync()).Items.Should().Be(1);
    }

    [Fact]
    public async Task Should_SwallowTheSave_WhenABoundaryIsOpen()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using var work = items.OpenUnitOfWork();
            await items.AddAsync(new Item { Name = "pen" }, Ct);

            (await items.SaveChangesAsync(Ct)).Should().Be(0);
            (await CountAsync()).Items.Should().Be(0);
        }
    }

    [Fact]
    public async Task Should_SwallowTheSave_WhenCalledDirectlyOnTheContext()
    {
        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await using var work = items.OpenUnitOfWork();
        context.Items.Add(new Item { Name = "pen" });

        (await context.SaveChangesAsync(Ct)).Should().Be(0);
        (await CountAsync()).Items.Should().Be(0);
    }

    [Fact]
    public async Task Should_WriteOnce_WhenTheRootIsApplied()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
            await using var work = items.OpenUnitOfWork();

            await items.AddAsync(new Item { Name = "pen" }, Ct);
            await items.SaveChangesAsync(Ct);
            await boxes.AddAsync(new Box { Label = "b1" }, Ct);
            await boxes.SaveChangesAsync(Ct);
            (await CountAsync()).Should().Be((0, 0));

            (await work.ApplyAsync(Ct)).Should().Be(2);
        }

        (await CountAsync()).Should().Be((1, 1));
    }

    [Fact]
    public async Task Should_WriteNothing_WhenDisposedWithoutApplying()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using (items.OpenUnitOfWork())
            {
                await items.AddAsync(new Item { Name = "pen" }, Ct);
                await items.SaveChangesAsync(Ct);
            }
        }

        (await CountAsync()).Items.Should().Be(0);
    }

    [Fact]
    public async Task Should_LeaveTheDatabaseUnchanged_WhenAnExceptionEscapesTheBoundary()
    {
        var act = async () =>
        {
            await using var scope = Db.NewScope();
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
            await using var work = items.OpenUnitOfWork();
            await items.AddAsync(new Item { Name = "pen" }, Ct);
            await items.SaveChangesAsync(Ct);
            await boxes.AddAsync(new Box { Label = "b1" }, Ct);
            await boxes.SaveChangesAsync(Ct);
            throw new InvalidOperationException("the operation fails half-way");
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("the operation fails half-way");
        (await CountAsync()).Should().Be((0, 0));
    }

    [Fact]
    public async Task Should_ClearTheTracker_WhenApplyFails()
    {
        await using (var seed = Db.NewContext())
        {
            seed.Boxes.Add(new Box { Label = "taken" });
            await seed.SaveChangesAsync(Ct);
        }

        await using var scope = Db.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();

        await using (var work = boxes.OpenUnitOfWork())
        {
            await items.AddAsync(new Item { Name = "lost" }, Ct);
            await boxes.AddAsync(new Box { Label = "taken" }, Ct);   // unique label: the apply fails
            await boxes.SaveChangesAsync(Ct);

            var apply = () => work.ApplyAsync(Ct);
            await apply.Should().ThrowAsync<DbUpdateException>();
        }

        context.ChangeTracker.Entries().Should().BeEmpty();

        await items.AddAsync(new Item { Name = "next" }, Ct);
        (await items.SaveChangesAsync(Ct)).Should().Be(1);

        await using var check = Db.NewContext();
        (await check.Items.Select(i => i.Name).ToListAsync(Ct)).Should().Equal("next");
    }

    [Fact]
    public async Task Should_BeASlave_WhenOpenedInsideAnotherBoundary()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            var boxes = scope.ServiceProvider.GetRequiredService<IRepository<Box>>();
            await using var outer = items.OpenUnitOfWork();
            await items.AddAsync(new Item { Name = "pen" }, Ct);
            await items.SaveChangesAsync(Ct);

            await using (var inner = boxes.OpenUnitOfWork())
            {
                await boxes.AddAsync(new Box { Label = "b1" }, Ct);
                await boxes.SaveChangesAsync(Ct);
                (await inner.ApplyAsync(Ct)).Should().Be(0);
            }

            (await CountAsync()).Should().Be((0, 0));
            (await outer.ApplyAsync(Ct)).Should().Be(2);
        }

        (await CountAsync()).Should().Be((1, 1));
    }

    [Fact]
    public async Task Should_Throw_WhenTheSameRootIsAppliedTwice()
    {
        await using var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await using var work = items.OpenUnitOfWork();
        await work.ApplyAsync(Ct);

        var again = () => work.ApplyAsync(Ct);

        await again.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_WriteAgain_WhenTheBoundaryWasApplied()
    {
        await using (var scope = Db.NewScope())
        {
            var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
            await using (var work = items.OpenUnitOfWork())
                await work.ApplyAsync(Ct);

            await items.AddAsync(new Item { Name = "pen" }, Ct);
            (await items.SaveChangesAsync(Ct)).Should().Be(1);
        }

        (await CountAsync()).Items.Should().Be(1);
    }
}
