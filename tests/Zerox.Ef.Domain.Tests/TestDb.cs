using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;
using Zerox.Ef.Db;

namespace Zerox.Ef.Domain.Tests;

public enum ItemKind
{
    Small,
    Large,
}

public sealed class Box : IEntity
{
    public int Id { get; set; }

    public string Label { get; set; } = "";

    public DateOnly Opened { get; set; }

    public List<Item> Items { get; set; } = [];
}

public sealed class Item : IEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public ItemKind Kind { get; set; }

    public DateTime At { get; set; }

    public bool Paid { get; set; }

    public string? Note { get; set; }

    public int? OwnerId { get; set; }

    // A reserved field: never named in a search, never listed.
    [JsonIgnore]
    public string Secret { get; set; } = "";

    // Not a column: it cannot be asked to the database.
    [NotMapped]
    public string Display => $"{Name} ({Quantity})";

    public int BoxId { get; set; }

    // A navigation stays readable through its fields even when it is [JsonIgnore].
    [JsonIgnore]
    public Box? Box { get; set; }
}

// The types an entity of the tests does not have (a Guid, a double, a long).
public sealed class Sample
{
    public Guid Code { get; set; }

    public double Weight { get; set; }

    public long Counter { get; set; }
}

public sealed class Memo : IEntity, IHasOptimisticLock
{
    public int Id { get; set; }

    public string Owner { get; set; } = "";

    public string Text { get; set; } = "";

    public string Note { get; set; } = "";

    public int Version { get; set; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Box> Boxes => Set<Box>();

    public DbSet<Memo> Memos => Set<Memo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock();
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
