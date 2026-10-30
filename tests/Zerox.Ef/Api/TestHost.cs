using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zerox.Ef.Core;
using Zerox.Ef.Db;
using Zerox.Ef.Tests;

namespace Zerox.Ef.Api.Tests;

public sealed class Doc : IEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    [Timestamp]
    public uint Version { get; set; }
}

public sealed class Plain : IEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class HostDbContext(DbContextOptions<HostDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Doc> Docs => Set<Doc>();

    public DbSet<Plain> Plains => Set<Plain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock(Database);
}

// A real WebApplication on TestServer with the error handling and the JSON of Zerox.Ef.Api, on the test database (Sqlite in memory, or PostgreSQL).
public sealed class TestHost : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private readonly WebApplication _app;

    private TestHost(TestDatabase database, WebApplication app, LogCapture logs)
    {
        _database = database;
        _app = app;
        Logs = logs;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public LogCapture Logs { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<TestHost> StartAsync(Action<WebApplication> map)
    {
        var database = await TestDatabase.OpenAsync();
        var logs = new LogCapture();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddZeroxDb<HostDbContext>(o => database.Use(o));
        builder.Services.AddControllers().AddZeroxJson();
        builder.Services.AddZeroxProblemDetails();

        var app = builder.Build();
        app.UseZeroxErrorHandling();
        map(app);

        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<HostDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        return new TestHost(database, app, logs);
    }

    // A context of its own on the same database.
    public HostDbContext NewContext() =>
        new(_database.Use(new DbContextOptionsBuilder<HostDbContext>()).UseZeroxSession().Options);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _database.DisposeAsync();
    }
}
