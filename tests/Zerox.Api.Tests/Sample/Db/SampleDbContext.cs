using Microsoft.EntityFrameworkCore;
using Zerox.Db;

namespace Zerox.Api.Tests.Sample.Db;

// All a project writes for its context: DbContext + IDbContext, its sets, its model.
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options), IDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sqlite cannot ORDER BY or SUM a decimal (EF Core: OrderByNotSupported,
        // AggregateOperationNotSupported); a real database keeps decimal.
        modelBuilder.Entity<Order>().Property(o => o.Amount).HasConversion<double>();
        modelBuilder.ApplyOptimisticLock();
    }
}
