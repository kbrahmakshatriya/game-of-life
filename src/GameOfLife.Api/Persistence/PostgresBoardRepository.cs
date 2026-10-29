using GameOfLife.Core;

using Npgsql;

using NpgsqlTypes;

namespace GameOfLife.Api.Persistence;

/// <summary>Stores boards in PostgreSQL: one row per board.</summary>
public sealed class PostgresBoardRepository(NpgsqlDataSource dataSource) : IBoardRepository
{
    private const string SelectBoard = "SELECT generation, status, period, cells FROM boards WHERE id = $1";

    public async Task<StoredBoard> AddAsync(Board board, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(board);

        var stored = new StoredBoard(Guid.NewGuid(), Generation: 0, BoardStatus.Active, Period: null, board);
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "INSERT INTO boards (id, generation, status, period, cells) VALUES ($1, $2, $3, $4, $5)");
        AddBoardParameters(command, stored);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return stored;
    }

    public async Task<StoredBoard?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(SelectBoard);
        command.Parameters.AddWithValue(id);
        return await ReadBoardAsync(command, id, cancellationToken);
    }

    private static async Task<StoredBoard?> ReadBoardAsync(NpgsqlCommand command, Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StoredBoard(
            id,
            Generation: reader.GetInt64(0),
            Status: Enum.Parse<BoardStatus>(reader.GetString(1)),
            Period: reader.IsDBNull(2) ? null : reader.GetInt32(2),
            Board: Board.Parse(reader.GetString(3)));
    }

    // Parameters $1 to $5 of the INSERT statement: id, generation, status, period and cells.
    private static void AddBoardParameters(NpgsqlCommand command, StoredBoard board)
    {
        command.Parameters.AddWithValue(board.Id);
        command.Parameters.AddWithValue(board.Generation);
        command.Parameters.AddWithValue(board.Status.ToString());
        command.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)board.Period ?? DBNull.Value);
        command.Parameters.AddWithValue(board.Board.ToString());
    }
}
