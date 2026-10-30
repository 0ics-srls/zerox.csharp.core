using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

using Zerox.Ef.Tests;

namespace Zerox.Ef.Db.Tests;

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

// The row version is the database's: EF's [Timestamp] on a uint (PostgreSQL: the system column xmin).
public sealed class Locked : IEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    [Timestamp]
    public uint Version { get; set; }
}

// A row version of a type PostgreSQL cannot generate: refused there.
public sealed class Wide : IEntity
{
    public int Id { get; set; }

    [Timestamp]
    public ulong Version { get; set; }
}

// A row version that is not a number: refused everywhere.
public sealed class Blob : IEntity
{
    public int Id { get; set; }

    [Timestamp]
    public byte[] Version { get; set; } = [];
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Box> Boxes => Set<Box>();

    public DbSet<Locked> Locked => Set<Locked>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Box>().HasIndex(b => b.Label).IsUnique();   // makes an apply fail on purpose
        modelBuilder.ApplyOptimisticLock(Database);
    }
}

// Model-only contexts: building their model is the test. EF builds a model once per context type, so a
// test that watches the building has a type of its own (RowVersionDbContext: only the warning tests).
public sealed class RowVersionDbContext(DbContextOptions<RowVersionDbContext> options) : DbContext(options)
{
    public DbSet<Locked> Locked => Set<Locked>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock(Database);
}

public sealed class WideDbContext(DbContextOptions<WideDbContext> options) : DbContext(options)
{
    public DbSet<Wide> Wides => Set<Wide>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock(Database);
}

public sealed class BlobDbContext(DbContextOptions<BlobDbContext> options) : DbContext(options)
{
    public DbSet<Blob> Blobs => Set<Blob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyOptimisticLock(Database);
}

// One database per test class (TestDatabase: Sqlite in memory, or PostgreSQL).
public sealed class TestDb : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private ServiceProvider? _provider;

    private TestDb(TestDatabase database) => _database = database;

    // The provider of this database on the options of a context a test builds by hand.
    public DbContextOptionsBuilder<TContext> Use<TContext>(DbContextOptionsBuilder<TContext> builder) where TContext : DbContext => _database.Use(builder);

    public DbContextOptionsBuilder Use(DbContextOptionsBuilder builder) => _database.Use(builder);

    public static async Task<TestDb> CreateAsync()
    {
        var db = new TestDb(await TestDatabase.OpenAsync());
        await using var context = db.NewContext();
        await context.Database.EnsureCreatedAsync();
        return db;
    }

    // A context of its own on the same database: to seed, and to check what is really written.
    public TestDbContext NewContext() =>
        new(_database.Use(new DbContextOptionsBuilder<TestDbContext>()).UseZeroxSession().Options);

    // A scope of a provider built once with AddZeroxDb, as an application has it.
    public AsyncServiceScope NewScope()
    {
        _provider ??= new ServiceCollection()
            .AddZeroxDb<TestDbContext>(o => _database.Use(o))
            .BuildServiceProvider();
        return _provider.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
        await _database.DisposeAsync();
    }
}
