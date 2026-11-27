using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Api.Tests.Sample.Api;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Api.Tests.Sample.Domain;
using Zerox.Ef.Core;
using Zerox.Ef.Db;
using Zerox.Ef.Domain;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Api.Tests.Sample;

// The example at the service level: the unit of work of the merge, the one-field rename, as results.
public sealed class CustomerServiceTests : IAsyncLifetime
{
    // Seeded ids: Acme = 1, Bolt = 2, Cargo = 3 (order i belongs to customer i % 3 → Bolt, Cargo, Acme…).
    private const int Acme = 1;
    private const int Bolt = 2;
    private const string PostgresOnly = "SQLite has no row version: the lock is proved on PostgreSQL.";

    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = await SampleApp.StartAsync();
        await App.SeedAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    private async Task<Dictionary<int, int>> OrdersPerCustomerAsync()
    {
        await using var check = App.NewContext();
        return await check.Orders.GroupBy(o => o.CustomerId).ToDictionaryAsync(g => g.Key, g => g.Count(), Ct);
    }

    private async Task<Customer> StoredAsync(int id)
    {
        await using var check = App.NewContext();
        return await check.Customers.AsNoTracking().SingleAsync(c => c.Id == id, Ct);
    }

    [Fact]
    public async Task Should_MoveOrdersAndDeleteTheCustomer_WhenMerged()
    {
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().MergeIntoAsync(Bolt, Acme, Ct);

        result.Status.Should().Be(OpStatus.Deleted);
        await using var check = App.NewContext();
        (await check.Customers.AnyAsync(c => c.Id == Bolt, Ct)).Should().BeFalse();
        (await check.Orders.CountAsync(o => o.CustomerId == Acme, Ct)).Should().Be(17);   // 8 + 9
    }

    [Fact]
    public async Task Should_WriteOnce_WhenMerged()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var writes = 0;
        context.SavedChanges += (_, e) => { if (e.EntitiesSavedCount > 0) writes++; };

        await scope.ServiceProvider.GetRequiredService<ICustomerService>().MergeIntoAsync(Bolt, Acme, Ct);

        writes.Should().Be(1);
    }

    [Fact]
    public async Task Should_WriteNothingAndReturnFaulted_WhenTheMergeFailsHalfway()
    {
        var before = await OrdersPerCustomerAsync();
        // Commands run only in the applying write; the orders are updated first, then the DELETE of the
        // customer fails and EF rolls back its own transaction.
        await using var provider = new ServiceCollection()
            .AddZeroxDb<SampleDbContext>(o => App.Use(o).AddInterceptors(new FailingDelete()))
            .AddZeroxDomain()
            .AddEntityService<Customer, ICustomerService, CustomerService>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().MergeIntoAsync(Bolt, Acme, Ct);

        result.Status.Should().Be(OpStatus.Faulted);
        result.Exception.Should().NotBeNull();
        (await OrdersPerCustomerAsync()).Should().BeEquivalentTo(before);
        await using var check = App.NewContext();
        (await check.Customers.CountAsync(Ct)).Should().Be(3);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundAndWriteNothing_WhenACustomerIsMissing()
    {
        var before = await OrdersPerCustomerAsync();
        await using var scope = App.Services.CreateAsyncScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var results = (await customers.MergeIntoAsync(Bolt, 404, Ct), await customers.MergeIntoAsync(404, Acme, Ct));

        results.Item1.Status.Should().Be(OpStatus.NotFound);
        results.Item2.Status.Should().Be(OpStatus.NotFound);
        (await OrdersPerCustomerAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Should_ReturnInvalid_WhenMergingACustomerIntoItself()
    {
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().MergeIntoAsync(Acme, Acme, Ct);

        (result.Status, result.Reason).Should().Be((OpStatus.Invalid, "A customer cannot be merged into itself."));
    }

    [Fact]
    public async Task Should_RenameOnlyTheName_WhenTheVersionIsTheOneRead()
    {
        var read = await StoredAsync(Acme);
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().RenameAsync(Acme, "Acme2", read.Version, Ct);

        result.Status.Should().Be(OpStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Acme2");
        var stored = await StoredAsync(Acme);
        (stored.Name, stored.City).Should().Be(("Acme2", read.City));
    }

    [Fact]
    public async Task Should_ReturnNotFound_WhenRenamingAMissingCustomer()
    {
        var versionOfAnother = (await StoredAsync(Acme)).Version;
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().RenameAsync(404, "x", versionOfAnother, Ct);

        result.Status.Should().Be(OpStatus.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_WhenRenamingAtAStaleVersion()
    {
        Assert.SkipUnless(TestDatabase.OnPostgres, PostgresOnly);
        var read = await StoredAsync(Acme);
        await using (var other = App.NewContext())
        {
            (await other.Customers.SingleAsync(c => c.Id == Acme, Ct)).City = "Turin";
            await other.SaveChangesAsync(Ct);
        }
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICustomerService>().RenameAsync(Acme, "Acme2", read.Version, Ct);

        result.Status.Should().Be(OpStatus.Conflict);
        (await StoredAsync(Acme)).Name.Should().Be(read.Name);
    }

    private sealed class FailingDelete : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("DELETE FROM", StringComparison.Ordinal)
                ? throw new InvalidOperationException("the delete fails")
                : ValueTask.FromResult(result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("DELETE FROM", StringComparison.Ordinal)
                ? throw new InvalidOperationException("the delete fails")
                : ValueTask.FromResult(result);
    }
}
