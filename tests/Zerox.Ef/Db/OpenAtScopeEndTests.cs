using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db.Tests;

// A unit of work or a massive operation still open when the scope ends is a mistake (an await using
// forgotten): its saves were swallowed or its transaction left open. The end of the scope says so.
public sealed class OpenAtScopeEndTests : IAsyncLifetime
{
    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    [Fact]
    public async Task Should_Throw_WhenTheScopeEndsWithAnOpenUnitOfWork()
    {
        var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        _ = items.OpenUnitOfWork();   // the await using forgotten
        await items.AddAsync(new Item { Name = "lost" }, Ct);
        (await items.SaveChangesAsync(Ct)).Should().Be(0);

        var end = async () => await scope.DisposeAsync();

        (await end.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("unit of work");
    }

    [Fact]
    public async Task Should_Throw_WhenTheScopeEndsWithAnOpenMassiveOperation()
    {
        var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        _ = await items.OpenMassiveAsync(Ct);   // the await using forgotten

        var end = async () => await scope.DisposeAsync();

        (await end.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("massive operation");
    }

    [Fact]
    public async Task Should_EndQuietly_WhenTheUnitOfWorkWasAppliedOrClosed()
    {
        var scope = Db.NewScope();
        var items = scope.ServiceProvider.GetRequiredService<IRepository<Item>>();
        await using (var uow = items.OpenUnitOfWork())
        {
            await items.AddAsync(new Item { Name = "kept" }, Ct);
            await items.SaveChangesAsync(Ct);
            await uow.ApplyAsync(Ct);
        }
        await using (items.OpenUnitOfWork())
        {
            await items.AddAsync(new Item { Name = "dropped" }, Ct);
        }

        var end = async () => await scope.DisposeAsync();

        await end.Should().NotThrowAsync();
        await using var check = Db.NewContext();
        (await check.Items.Select(i => i.Name).ToListAsync(Ct)).Should().Equal("kept");
    }
}
