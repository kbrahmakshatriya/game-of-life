using System.Diagnostics;

using GameOfLife.Api.Configuration;
using GameOfLife.Api.Persistence;
using GameOfLife.Core;

using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Boards;

/// <summary>Changes stored boards: creating them, advancing them, and settling them into their final state.</summary>
/// <remarks>
/// Each change to an existing board locks it first, so changes to one board happen one at a time: a second request
/// waits for the first to finish and then starts from what it saved. Nothing is saved unless the change completes.
/// </remarks>
public sealed partial class BoardService(
    IBoardRepository repository, IOptions<GameOfLifeOptions> options, ILogger<BoardService> logger)
{
    /// <summary>Stores <paramref name="board"/> as generation 0 of a new, active board.</summary>
    public async Task<StoredBoard> CreateAsync(Board board, CancellationToken cancellationToken)
    {
        StoredBoard stored = await repository.AddAsync(board, cancellationToken);
        LogCreated(logger, stored.Id, board.Rows, board.Columns, board.LiveCellCount);
        return stored;
    }

    /// <summary>Advances the board <paramref name="generations"/> generations and saves it, unless it has settled.</summary>
    public async Task<BoardUpdateResult> AdvanceAsync(Guid id, int generations, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generations);

        await using IBoardLock? locked = await repository.LockAsync(id, cancellationToken);
        if (locked is null)
        {
            return new BoardUpdateResult.NotFound();
        }

        StoredBoard current = locked.Board;
        if (current.Status != BoardStatus.Active)
        {
            return new BoardUpdateResult.AlreadySettled(current);
        }

        StoredBoard advanced = current with
        {
            Generation = current.Generation + generations,
            Board = Simulation.Advance(current.Board, generations, cancellationToken),
        };
        await locked.SaveAsync(advanced, cancellationToken);
        LogAdvanced(logger, id, current.Generation, advanced.Generation);
        return new BoardUpdateResult.Succeeded(advanced);
    }

    /// <summary>
    /// Runs the board forward from its current generation until a generation repeats an earlier one, then saves the
    /// first generation of that repeating cycle as the board's final state. The search starts at the current
    /// generation, so a cycle that began before it is saved as starting there. A settled board is returned unchanged,
    /// so repeating the request is harmless.
    /// </summary>
    public async Task<BoardUpdateResult> SettleAsync(Guid id, CancellationToken cancellationToken)
    {
        await using IBoardLock? locked = await repository.LockAsync(id, cancellationToken);
        if (locked is null)
        {
            return new BoardUpdateResult.NotFound();
        }

        StoredBoard current = locked.Board;
        if (current.Status != BoardStatus.Active)
        {
            return new BoardUpdateResult.Succeeded(current);
        }

        int maxGenerations = options.Value.MaxGenerations;
        FinalState? finalState = Simulation.FindFinalState(current.Board, maxGenerations, cancellationToken);
        if (finalState is null)
        {
            // Returning without saving releases the lock and leaves the board as it was.
            LogDidNotSettle(logger, id, maxGenerations, current.Generation);
            return new BoardUpdateResult.DidNotSettle(maxGenerations);
        }

        // The search counts generations from the current one, so add them to get the board's generation number.
        StoredBoard settled = current with
        {
            Generation = current.Generation + finalState.Generation,
            Status = ToStatus(finalState.Kind),
            Period = finalState.Period,
            Board = finalState.Board,
        };
        await locked.SaveAsync(settled, cancellationToken);
        LogSettled(logger, id, settled.Generation, settled.Status, finalState.Period);
        return new BoardUpdateResult.Succeeded(settled);
    }

    private static BoardStatus ToStatus(FinalStateKind kind) => kind switch
    {
        FinalStateKind.Extinct => BoardStatus.Extinct,
        FinalStateKind.StillLife => BoardStatus.StillLife,
        FinalStateKind.Oscillator => BoardStatus.Oscillator,
        _ => throw new UnreachableException($"FinalStateKind {kind} has no matching BoardStatus."),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Created board {BoardId}: {Rows} x {Columns} cells, {LiveCells} alive")]
    private static partial void LogCreated(ILogger logger, Guid boardId, int rows, int columns, int liveCells);

    [LoggerMessage(Level = LogLevel.Information, Message = "Advanced board {BoardId} from generation {FromGeneration} to {ToGeneration}")]
    private static partial void LogAdvanced(ILogger logger, Guid boardId, long fromGeneration, long toGeneration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Board {BoardId} settled at generation {Generation} as {Status} with period {Period}")]
    private static partial void LogSettled(ILogger logger, Guid boardId, long generation, BoardStatus status, int period);

    [LoggerMessage(Level = LogLevel.Information, Message = "Board {BoardId} did not settle within {MaxGenerations} generations of generation {Generation}")]
    private static partial void LogDidNotSettle(ILogger logger, Guid boardId, int maxGenerations, long generation);
}
