using GameOfLife.Api.Persistence;

using Npgsql;

using Testcontainers.PostgreSql;

namespace GameOfLife.Api.Tests;

// One PostgreSQL container in Docker for the whole test run. Every test host gets a database of its own, so hosts
// never see each other's boards.
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string CollectionName = "PostgreSQL";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    // Creates a database with the boards table and returns a connection string to it.
    public async Task<string> CreateDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString());
        string name = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            // A database name cannot be a query parameter. This one is generated above, never taken from input.
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = name;
        await using (NpgsqlDataSource dataSource = NpgsqlDataSource.Create(builder.ConnectionString))
        {
            // Created here, before any host starts, so hosts starting together never race to create it.
            await BoardsTable.EnsureCreatedAsync(dataSource, CancellationToken.None);
        }

        return builder.ConnectionString;
    }
}

// The test classes that use the database share the one container through this collection.
[CollectionDefinition(PostgresFixture.CollectionName)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>;
