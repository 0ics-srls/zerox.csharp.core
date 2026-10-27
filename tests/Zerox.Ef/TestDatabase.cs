using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Zerox.Ef.Tests;

// The database of one test class, shared by the three Zerox.Ef test projects. Sqlite in memory by
// default (alive while its connection is open); a fresh PostgreSQL database, dropped at the end, when
// ZEROX_EF_TEST_POSTGRES holds the connection string of a server (tools/test-postgres.sh starts one in
// Docker and runs the Ef tests on it).
public sealed class TestDatabase : IAsyncDisposable
{
    private const string PostgresServer = "ZEROX_EF_TEST_POSTGRES";

    private readonly SqliteConnection? _sqlite;
    private readonly string? _postgres;

    private TestDatabase(SqliteConnection? sqlite, string? postgres)
    {
        _sqlite = sqlite;
        _postgres = postgres;
    }

    public static bool OnPostgres => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PostgresServer));

    public static async Task<TestDatabase> OpenAsync()
    {
        var server = Environment.GetEnvironmentVariable(PostgresServer);
        if (string.IsNullOrEmpty(server))
        {
            var sqlite = new SqliteConnection("DataSource=:memory:");
            await sqlite.OpenAsync();
            return new TestDatabase(sqlite, null);
        }
        var database = new NpgsqlConnectionStringBuilder(server) { Database = $"zerox_{Guid.NewGuid():N}" };
        return new TestDatabase(null, database.ConnectionString);
    }

    // The provider of this database on the options of a context.
    public DbContextOptionsBuilder Use(DbContextOptionsBuilder builder) =>
        _sqlite is not null ? builder.UseSqlite(_sqlite) : builder.UseNpgsql(_postgres);

    public DbContextOptionsBuilder<TContext> Use<TContext>(DbContextOptionsBuilder<TContext> builder) where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)Use((DbContextOptionsBuilder)builder);

    public async ValueTask DisposeAsync()
    {
        if (_sqlite is not null)
        {
            await _sqlite.DisposeAsync();
            return;
        }
        var server = new NpgsqlConnectionStringBuilder(_postgres) { Pooling = false };
        var name = server.Database;
        server.Database = "postgres";
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(server.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
