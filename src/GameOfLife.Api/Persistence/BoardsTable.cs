using Npgsql;

namespace GameOfLife.Api.Persistence;

/// <summary>The boards table, which the app creates at startup when it is missing.</summary>
/// <remarks>
/// CREATE TABLE IF NOT EXISTS is enough for one app instance. Several instances starting at the same moment against
/// an empty database could race to create the table, so a deployment like that should create it in a separate step.
/// PostgreSQL also checks permission to create tables before it checks whether the table exists, so a role without
/// that permission cannot start the app even when the table is already there.
/// </remarks>
public static class BoardsTable
{
    private const string CreateSql = """
        CREATE TABLE IF NOT EXISTS boards (
            id         uuid        PRIMARY KEY,
            generation bigint      NOT NULL CHECK (generation >= 0),
            status     text        NOT NULL CHECK (status IN ('Active', 'Extinct', 'StillLife', 'Oscillator')),
            -- How many generations a settled board's cycle takes; null while the board is active.
            period     integer     CHECK (period >= 1),
            -- One line per row: 'O' for a live cell, '.' for a dead one.
            cells      text        NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            CHECK ((status = 'Active') = (period IS NULL))
        )
        """;

    /// <summary>Creates the boards table unless it already exists.</summary>
    public static async Task EnsureCreatedAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        await using NpgsqlCommand command = dataSource.CreateCommand(CreateSql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
