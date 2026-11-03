using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Api.Tests.Sample.Api;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Core;

namespace Zerox.Ef.Api.Tests.Sample;

// The example of a massive operation at the service level: many rows updated without loading them.
public sealed class OrderServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Cut = new(2026, 1, 11);

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
    public async Task Should_CloseOnlyOpenOrdersBeforeTheDate()
    {
        int openBefore, closedAfter;
        await using (var seed = App.NewContext())
        {
            openBefore = await seed.Orders.CountAsync(o => o.Date < Cut && o.Status == OrderStatus.Open, Ct);
            closedAfter = await seed.Orders.CountAsync(o => o.Date >= Cut && o.Status == OrderStatus.Closed, Ct);
        }
        await using var scope = App.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IOrderService>().CloseBeforeAsync(Cut, Ct);

        (result.Status, result.Value).Should().Be((OpStatus.Ok, openBefore));
        await using var check = App.NewContext();
        (await check.Orders.CountAsync(o => o.Date < Cut && o.Status == OrderStatus.Open, Ct)).Should().Be(0);
        (await check.Orders.CountAsync(o => o.Date >= Cut && o.Status == OrderStatus.Closed, Ct)).Should().Be(closedAfter);
    }
}
