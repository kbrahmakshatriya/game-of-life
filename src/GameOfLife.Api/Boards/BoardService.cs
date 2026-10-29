using GameOfLife.Api.Persistence;
using GameOfLife.Core;

namespace GameOfLife.Api.Boards;

/// <summary>Changes stored boards, starting with creating them.</summary>
public sealed partial class BoardService(IBoardRepository repository, ILogger<BoardService> logger)
{
    /// <summary>Stores <paramref name="board"/> as generation 0 of a new, active board.</summary>
    public async Task<StoredBoard> CreateAsync(Board board, CancellationToken cancellationToken)
    {
        StoredBoard stored = await repository.AddAsync(board, cancellationToken);
        LogCreated(logger, stored.Id, board.Rows, board.Columns, board.LiveCellCount);
        return stored;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created board {BoardId}: {Rows} x {Columns} cells, {LiveCells} alive")]
    private static partial void LogCreated(ILogger logger, Guid boardId, int rows, int columns, int liveCells);
}
