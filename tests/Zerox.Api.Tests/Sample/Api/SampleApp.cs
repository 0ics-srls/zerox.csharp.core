using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Api.Tests.Sample.Db;
using Zerox.Db;

namespace Zerox.Api.Tests.Sample.Api;

// The example application: what a project writes in its Program.cs, on TestServer with Sqlite in memory.
public sealed class SampleApp : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplication _app;

    private SampleApp(SqliteConnection connection, WebApplication app)
    {
        _connection = connection;
        _app = app;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<SampleApp> StartAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddZeroxDb<SampleDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddControllers().AddApplicationPart(typeof(SampleApp).Assembly).AddZeroxJson();

        var app = builder.Build();
        app.UseZeroxErrorHandling();
        app.MapControllers();

        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        return new SampleApp(connection, app);
    }

    // A context of its own on the same database: to seed and to check what is really written.
    public SampleDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SampleDbContext>().UseSqlite(_connection).UseZeroxSession().Options);

    // 3 customers (Acme/Rome, Bolt/Milan, Cargo/no city) and 25 orders: order i belongs to customer i % 3,
    // Amount = i * 10, Closed when i % 5 == 0, Date = 2026-01-01 + i days.
    public async Task SeedAsync()
    {
        await using var seed = NewContext();
        Customer[] customers = [new() { Name = "Acme", City = "Rome" }, new() { Name = "Bolt", City = "Milan" }, new() { Name = "Cargo", City = "" }];
        seed.Customers.AddRange(customers);
        for (var i = 1; i <= 25; i++)
        {
            seed.Orders.Add(new Order
            {
                Customer = customers[i % 3],
                Amount = i * 10,
                Status = i % 5 == 0 ? OrderStatus.Closed : OrderStatus.Open,
                Date = new DateOnly(2026, 1, 1).AddDays(i),
                InternalNote = "internal",
            });
        }
        await seed.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
