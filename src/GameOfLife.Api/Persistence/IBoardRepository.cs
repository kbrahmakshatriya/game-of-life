using GameOfLife.Core;

namespace GameOfLife.Api.Persistence;

/// <summary>Stores boards: one current version of each, replaced whenever the board changes.</summary>
public interface IBoardRepository
{
    /// <summary>Stores <paramref name="board"/> as generation 0 of a new, active board, and returns it.</summary>
    Task<StoredBoard> AddAsync(Board board, CancellationToken cancellationToken);

    /// <summary>Returns the board stored under <paramref name="id"/>, or <see langword="null"/> if there is none.</summary>
    Task<StoredBoard?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the board stored under <paramref name="id"/> against other changes and returns it, first waiting while
    /// another change holds it; or returns <see langword="null"/> if there is no such board.
    /// </summary>
    Task<IBoardLock?> LockAsync(Guid id, CancellationToken cancellationToken);
}
