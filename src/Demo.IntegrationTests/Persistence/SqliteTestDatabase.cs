using Demo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Demo.IntegrationTests.Persistence;

/// <summary>
/// A real SQLite database living in memory for the lifetime of one test, migrated and seeded exactly like the app's.
/// The connection stays open because an in-memory SQLite database disappears when its last connection closes.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DemoDbContext> _options;

    private SqliteTestDatabase(SqliteConnection connection)
    {
        _connection = connection;

        var builder = new DbContextOptionsBuilder<DemoDbContext>();
        builder.UseSqlite(connection).UseDemoSeeding();
        _options = builder.Options;
    }

    public static async Task<SqliteTestDatabase> CreateMigratedAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var database = new SqliteTestDatabase(connection);
        await using var dbContext = database.CreateContext();
        await dbContext.Database.MigrateAsync();

        return database;
    }

    public DemoDbContext CreateContext() => new(_options);

    /// <summary>Raw SQL, to assert on what is physically stored rather than on what EF Core maps back.</summary>
    public async Task<object?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
