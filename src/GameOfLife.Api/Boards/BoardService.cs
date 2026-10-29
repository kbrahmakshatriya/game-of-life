using GameOfLife.Api.Persistence;
using GameOfLife.Core;

namespace GameOfLife.Api.Boards;

/// <summary>Changes stored boards: creating them and advancing them.</summary>
/// <remarks>
/// Each change to an existing board locks it first, so changes to one board happen one at a time: a second request
/// waits for the first to finish and then starts from what it saved. Nothing is saved unless the change completes.
/// </remarks>
public sealed partial class BoardService(IBoardRepository repository, ILogger<BoardService> logger)
{
    /// <summary>Stores <paramref name="board"/> as generation 0 of a new, active board.</summary>
    public async Task<StoredBoard> CreateAsync(Board board, CancellationToken cancellationToken)
    {
        StoredBoard stored = await repository.AddAsync(board, cancellationToken);
        LogCreated(logger, stored.Id, board.Rows, board.Columns, board.LiveCellCount);
        return stored;
    }

    /// <summary>Advances the board <paramref name="generations"/> generations and saves it.</summary>
    public async Task<BoardUpdateResult> AdvanceAsync(Guid id, int generations, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generations);

        await using IBoardLock? locked = await repository.LockAsync(id, cancellationToken);
        if (locked is null)
        {
            return new BoardUpdateResult.NotFound();
        }

        StoredBoard current = locked.Board;
        StoredBoard advanced = current with
        {
            Generation = current.Generation + generations,
            Board = Simulation.Advance(current.Board, generations, cancellationToken),
        };
        await locked.SaveAsync(advanced, cancellationToken);
        LogAdvanced(logger, id, current.Generation, advanced.Generation);
        return new BoardUpdateResult.Succeeded(advanced);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created board {BoardId}: {Rows} x {Columns} cells, {LiveCells} alive")]
    private static partial void LogCreated(ILogger logger, Guid boardId, int rows, int columns, int liveCells);

    [LoggerMessage(Level = LogLevel.Information, Message = "Advanced board {BoardId} from generation {FromGeneration} to {ToGeneration}")]
    private static partial void LogAdvanced(ILogger logger, Guid boardId, long fromGeneration, long toGeneration);
}
