namespace GameOfLife.Api.Persistence;

/// <summary>
/// A board locked against other changes. The lock lasts until the change is saved, or until this is disposed without
/// saving, which abandons the change and leaves the board as it was.
/// </summary>
public interface IBoardLock : IAsyncDisposable
{
    /// <summary>Gets the board as it was when the lock was taken.</summary>
    StoredBoard Board { get; }

    /// <summary>Saves <paramref name="updated"/> in place of the board and releases the lock.</summary>
    Task SaveAsync(StoredBoard updated, CancellationToken cancellationToken);
}
