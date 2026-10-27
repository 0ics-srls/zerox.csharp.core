using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Api.Tests.Sample.Api;
using Zerox.Api.Tests.Sample.Db;
using Zerox.Core.Data;

namespace Zerox.Api.Tests.Sample;

// The example at the database level: the repositories of the sample application.
public sealed class SampleRepositoryTests : IAsyncLifetime
{
    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = await SampleApp.StartAsync();
        await App.SeedAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    [Fact]
    public async Task Should_ReadTheSeededRows_ThroughTheRepository()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IRepository<Order>>();

        (await orders.Query().CountAsync(o => o.Customer!.Name == "Cargo", Ct)).Should().Be(8);
        (await orders.Query().CountAsync(Ct)).Should().Be(25);
    }

    [Fact]
    public async Task Should_RollBack_WhenTheMassiveOperationIsNotCommitted()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<IRepository<Order>>();
            await using var massive = await orders.OpenMassiveAsync(Ct);
            (await massive.UpdateWhereAsync(o => o.Status == OrderStatus.Open, f => f.Set(o => o.Status, OrderStatus.Closed), Ct))
                .Should().Be(20);
        }

        await using var check = App.NewContext();
        (await check.Orders.CountAsync(o => o.Status == OrderStatus.Open, Ct)).Should().Be(20);
    }
}
