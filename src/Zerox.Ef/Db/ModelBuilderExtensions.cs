using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Zerox.Ef.Db;

/// <summary>
/// The library's part of a context's model.
/// </summary>
public static class ModelBuilderExtensions
{
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private static readonly Type[] NumericRowVersions = [typeof(uint), typeof(ulong), typeof(long)];

    /// <summary>
    /// Shapes the optimistic lock. Call it at the END of OnModelCreating with the context's Database. An
    /// entity opts in with EF's [Timestamp] on a property of the type its database generates — uint on
    /// PostgreSQL (the system column xmin), ulong or long on SQL Server (rowversion): the database writes it,
    /// EF reads it back, nothing of ours sets it. Any other type is refused (NotSupportedException). SQLite
    /// generates no row version: there each gets the default 0 and one warning says the lock is off.
    /// </summary>
    public static ModelBuilder ApplyOptimisticLock(this ModelBuilder modelBuilder, DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(database);

        var rowVersions = RowVersions(modelBuilder.Model).ToList();
        foreach (var property in rowVersions)
            Check(property, database.ProviderName);

        if (rowVersions.Count > 0 && database.ProviderName == SqliteProvider)
        {
            foreach (var property in rowVersions)
                property.SetDefaultValue(Activator.CreateInstance(property.ClrType));
            database.GetService<ILoggerFactory>().CreateLogger("Zerox.Ef.Db").LogWarning(
                "SQLite generates no row version: the optimistic lock of {Entities} is off, every save goes through.",
                string.Join(", ", rowVersions.Select(p => p.DeclaringType.ClrType.Name).Distinct()));
        }
        return modelBuilder;
    }

    // A row version as EF defines it (IsRowVersion, [Timestamp]): a concurrency token generated on add and update.
    private static IEnumerable<IMutableProperty> RowVersions(IMutableModel model) =>
        model.GetEntityTypes().SelectMany(t => t.GetDeclaredProperties())
            .Where(p => p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate);

    private static void Check(IMutableProperty property, string? provider)
    {
        var name = $"{property.DeclaringType.ClrType.Name}.{property.Name}";
        if (!NumericRowVersions.Contains(property.ClrType))
            throw new NotSupportedException(
                $"{name}: a row version travels to the client as a number — uint on PostgreSQL, ulong on SQL Server — not {property.ClrType.Name}.");
        if (provider == PostgresProvider && property.ClrType != typeof(uint))
            throw new NotSupportedException($"{name}: on PostgreSQL the row version is a uint (the system column xmin).");
    }
}
