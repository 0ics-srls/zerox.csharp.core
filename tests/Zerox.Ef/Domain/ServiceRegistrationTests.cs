using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;
using Zerox.Ef.Db;

namespace Zerox.Ef.Domain.Tests;

// Every entity has a service; a derived service is served under its own contract and the base's, one
// instance per scope.
public sealed class ServiceRegistrationTests : IAsyncLifetime
{
    private static readonly ServiceProviderOptions Checked = new() { ValidateScopes = true, ValidateOnBuild = true };

    private TestDb? _database;

    private TestDb Db => _database ?? throw new InvalidOperationException("InitializeAsync has not run.");

    public async ValueTask InitializeAsync() => _database = await TestDb.CreateAsync();

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    public interface IItemService : IEntityService<Item>;

    public sealed class ItemService(IRepository<Item> repository) : EntityService<Item>(repository), IItemService;

    private IServiceCollection Services() => new ServiceCollection().AddZeroxDb<TestDbContext>(o => Db.Use(o));

    [Fact]
    public async Task Should_ServeEveryEntity_WhenAddZeroxDomainIsCalled()
    {
        await using var provider = Services().AddZeroxDomain().BuildServiceProvider(Checked);
        await using var scope = provider.CreateAsyncScope();

        var services = (Items: scope.ServiceProvider.GetRequiredService<IEntityService<Item>>(), Memos: scope.ServiceProvider.GetRequiredService<IEntityService<Memo>>());

        services.Items.Should().BeOfType<EntityService<Item>>();
        services.Memos.Should().BeOfType<EntityService<Memo>>();
    }

    [Fact]
    public async Task Should_GiveOneInstancePerScope()
    {
        await using var provider = Services().AddZeroxDomain().BuildServiceProvider(Checked);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        var resolved = (
            A: first.ServiceProvider.GetRequiredService<IEntityService<Item>>(),
            B: first.ServiceProvider.GetRequiredService<IEntityService<Item>>(),
            C: second.ServiceProvider.GetRequiredService<IEntityService<Item>>());

        resolved.A.Should().BeSameAs(resolved.B);
        resolved.A.Should().NotBeSameAs(resolved.C);
    }

    [Fact]
    public async Task Should_ServeTheDerivedService_UnderBothContracts()
    {
        await using var provider = Services().AddZeroxDomain().AddEntityService<Item, IItemService, ItemService>().BuildServiceProvider(Checked);
        await using var scope = provider.CreateAsyncScope();

        var resolved = (Own: scope.ServiceProvider.GetRequiredService<IItemService>(), Base: scope.ServiceProvider.GetRequiredService<IEntityService<Item>>());

        resolved.Own.Should().BeOfType<ItemService>();
        resolved.Base.Should().BeSameAs(resolved.Own);
    }

    [Fact]
    public async Task Should_ServeTheDerivedService_WhenItHasNoContractOfItsOwn()
    {
        var services = Services().AddZeroxDomain().AddEntityService<Item, IEntityService<Item>, ItemService>();
        await using var provider = services.BuildServiceProvider(Checked);
        await using var scope = provider.CreateAsyncScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IEntityService<Item>>();

        resolved.Should().BeOfType<ItemService>();
        services.Count(d => d.ServiceType == typeof(IEntityService<Item>)).Should().Be(1);
    }

    [Fact]
    public async Task Should_KeepTheDerivedService_WhenAddZeroxDomainIsCalledAfterIt()
    {
        await using var provider = Services().AddEntityService<Item, IItemService, ItemService>().AddZeroxDomain().BuildServiceProvider(Checked);
        await using var scope = provider.CreateAsyncScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IEntityService<Item>>();

        resolved.Should().BeOfType<ItemService>();
    }

    [Fact]
    public void Should_PassTheValidation_WhenBuiltWithScopeChecks()
    {
        var services = Services().AddZeroxDomain().AddEntityService<Item, IItemService, ItemService>();

        var build = () => services.BuildServiceProvider(Checked).Dispose();

        build.Should().NotThrow();
    }

    [Fact]
    public void Should_ThrowArgumentNull_WhenTheServicesAreNull()
    {
        var calls = new Action[]
        {
            // A guard test: null goes into a parameter that is not nullable (the rule's own exception for tests).
            // ast-grep-ignore: no-null-forgiving
            () => Domain.ServiceCollectionExtensions.AddZeroxDomain(null!),
            // ast-grep-ignore: no-null-forgiving
            () => Domain.ServiceCollectionExtensions.AddEntityService<Item, IItemService, ItemService>(null!),
        };

        calls.Should().AllSatisfy(call => call.Should().Throw<ArgumentNullException>());
    }
}
