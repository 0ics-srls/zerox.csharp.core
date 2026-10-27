using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Core.Data;

namespace Zerox.Db.Tests;

public sealed class Item : IEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Quantity { get; set; }

    public int? BoxId { get; set; }

    public Box? Box { get; set; }
}

public sealed class Box : IEntity
{
    public int Id { get; set; }

    public string Label { get; set; } = "";

    public List<Item> Items { get; set; } = [];
}

public sealed class Locked : IEntity, IHasOptimisticLock
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Version { get; set; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Box> Boxes => Set<Box>();

    public DbSet<Locked> Locked => Set<Locked>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Box>().HasIndex(b => b.Label).IsUnique();   // makes an apply fail on purpose
        modelBuilder.ApplyOptimisticLock();
    }
}

// One Sqlite database in memory per test class: alive while the connection is open.
public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider? _provider;

    private TestDb()
    {
    }

    public SqliteConnection Connection => _connection;

    public static async Task<TestDb> CreateAsync()
    {
        var db = new TestDb();
        await db._connection.OpenAsync();
        await using var context = db.NewContext();
        await context.Database.EnsureCreatedAsync();
        return db;
    }

    // A context of its own on the same database: to seed, and to check what is really written.
    public TestDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).UseZeroxSession().Options);

    // A scope of a provider built once with AddZeroxDb, as an application has it.
    public AsyncServiceScope NewScope()
    {
        _provider ??= new ServiceCollection()
            .AddZeroxDb<TestDbContext>(o => o.UseSqlite(_connection))
            .BuildServiceProvider();
        return _provider.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
