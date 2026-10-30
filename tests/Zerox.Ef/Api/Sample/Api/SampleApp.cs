using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Zerox.Ef.Api.Tests.Sample.Core;
using Zerox.Ef.Api.Tests.Sample.Db;
using Zerox.Ef.Api.Tests.Sample.Domain;
using Zerox.Ef.Db;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Api.Tests.Sample.Api;

// The example application: what a project writes in its Program.cs, in Development, with the test
// database — on TestServer, or on a real port for a client outside the process (the TypeScript one).
public sealed class SampleApp : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private readonly WebApplication _app;

    private SampleApp(TestDatabase database, WebApplication app, HttpClient client)
    {
        _database = database;
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    // The provider of the database, for a test that builds its own provider on the same database.
    public DbContextOptionsBuilder Use(DbContextOptionsBuilder builder) => _database.Use(builder);

    public IServiceProvider Services => _app.Services;

    // The address the application listens on, when it was started on a real port.
    public Uri Address => Client.BaseAddress ?? throw new InvalidOperationException("The application runs on TestServer.");

    public static async Task<SampleApp> StartAsync(bool onARealPort = false)
    {
        var database = await TestDatabase.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        if (onARealPort)
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        else
            builder.WebHost.UseTestServer();
        builder.Services.AddZeroxDb<SampleDbContext>(o => database.Use(o));
        builder.Services.AddScoped<ICustomerMerge, CustomerMerge>();
        builder.Services.AddControllers().AddApplicationPart(typeof(SampleApp).Assembly).AddZeroxJson();
        builder.Services.AddZeroxProblemDetails();
        builder.Services.AddZeroxOpenApi();

        var app = builder.Build();
        app.UseZeroxErrorHandling();
        app.MapControllers();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        var client = onARealPort
            ? new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }
            : app.GetTestClient();
        return new SampleApp(database, app, client);
    }

    // A context of its own on the same database: to seed and to check what is really written.
    public SampleDbContext NewContext() =>
        new(_database.Use(new DbContextOptionsBuilder<SampleDbContext>()).UseZeroxSession().Options);

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
        await _database.DisposeAsync();
    }
}
