using GameOfLife.Core;

using Npgsql;

using NpgsqlTypes;

namespace GameOfLife.Api.Persistence;

/// <summary>Stores boards in PostgreSQL: one row per board, overwritten whenever the board changes.</summary>
/// <remarks>
/// A change locks the board's row with SELECT ... FOR UPDATE and keeps the lock until it saves or gives up, so
/// changes to one board happen one at a time. Other boards, and plain reads of any board, are never blocked.
/// </remarks>
public sealed class PostgresBoardRepository(NpgsqlDataSource dataSource) : IBoardRepository
{
    private const string SelectBoard = "SELECT generation, status, period, cells FROM boards WHERE id = $1";
    private const string SelectBoardForUpdate = SelectBoard + " FOR UPDATE";

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

    public async Task<IBoardLock?> LockAsync(Guid id, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        NpgsqlTransaction? transaction = null;
        StoredBoard? board = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(SelectBoardForUpdate, connection, transaction);
            command.Parameters.AddWithValue(id);
            // FOR UPDATE waits here while another change holds this board's row lock.
            board = await ReadBoardAsync(command, id, cancellationToken);
        }
        finally
        {
            if (board is null)
            {
                // No such board, or the read failed: end the transaction and return the connection to the pool.
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }

                await connection.DisposeAsync();
            }
        }

        // A board was read, so the transaction was started; the lock now owns it and the connection.
        return board is null ? null : new PostgresBoardLock(connection, transaction!, board);
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

    // Parameters $1 to $5 of the INSERT and UPDATE statements: id, generation, status, period and cells.
    private static void AddBoardParameters(NpgsqlCommand command, StoredBoard board)
    {
        command.Parameters.AddWithValue(board.Id);
        command.Parameters.AddWithValue(board.Generation);
        command.Parameters.AddWithValue(board.Status.ToString());
        command.Parameters.AddWithValue(NpgsqlDbType.Integer, (object?)board.Period ?? DBNull.Value);
        command.Parameters.AddWithValue(board.Board.ToString());
    }

    // Owns the connection and the transaction that hold one board's row lock until the change is saved or abandoned.
    private sealed class PostgresBoardLock(NpgsqlConnection connection, NpgsqlTransaction transaction, StoredBoard board)
        : IBoardLock
    {
        public StoredBoard Board { get; } = board;

        public async Task SaveAsync(StoredBoard updated, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(updated);
            if (updated.Id != Board.Id)
            {
                throw new ArgumentException("A board can only be replaced by a later version of itself.", nameof(updated));
            }

            await using var command = new NpgsqlCommand(
                "UPDATE boards SET generation = $2, status = $3, period = $4, cells = $5, updated_at = now() WHERE id = $1",
                connection,
                transaction);
            AddBoardParameters(command, updated);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Disposing a transaction that was not committed rolls it back, which releases the row lock. Disposing the
        // connection returns it to the pool.
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
