namespace GameOfLife.Core;

/// <summary>Runs a board forward through many generations.</summary>
public static class Simulation
{
    /// <summary>
    /// Returns the board <paramref name="generations"/> generations after <paramref name="board"/>.
    /// </summary>
    /// <param name="board">The starting board.</param>
    /// <param name="generations">How many generations to advance; 0 returns <paramref name="board"/> itself.</param>
    /// <param name="cancellationToken">Checked before each generation is computed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="generations"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static Board Advance(Board board, int generations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfNegative(generations);

        Board current = board;
        for (int generation = 0; generation < generations; generation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = current.NextGeneration();
        }

        return current;
    }

    /// <summary>
    /// Runs <paramref name="board"/> forward until a generation repeats an earlier one. The rules are deterministic,
    /// so from then on the board cycles through the same generations forever: that cycle is its final state.
    /// </summary>
    /// <param name="board">The starting board, generation 0.</param>
    /// <param name="maxGenerations">The most generations to compute before giving up, at least 1.</param>
    /// <param name="cancellationToken">Checked before each generation is computed.</param>
    /// <returns>
    /// The final state, or <see langword="null"/> when no generation repeats within <paramref name="maxGenerations"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Every generation computed is kept and compared exactly, so a repeat is never missed or falsely reported, as
    /// could happen when comparing hashes alone. The cost is memory: up to <paramref name="maxGenerations"/> boards,
    /// at one bit per cell.
    /// </para>
    /// <para>
    /// A board that stops changing at generation g is only seen to have stopped when generation g + 1 repeats it, so
    /// that takes g + 1 generations of the limit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxGenerations"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static FinalState? FindFinalState(Board board, int maxGenerations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGenerations);

        // Every generation seen so far, with the generation number at which it first appeared.
        var firstSeenAt = new Dictionary<Board, int> { [board] = 0 };
        Board current = board;

        // Counting steps from 0 keeps the loop from overflowing when maxGenerations is int.MaxValue.
        for (int step = 0; step < maxGenerations; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = current.NextGeneration();
            int generation = step + 1;

            if (firstSeenAt.TryGetValue(current, out int cycleStart))
            {
                return new FinalState(current, cycleStart, generation - cycleStart);
            }

            firstSeenAt.Add(current, generation);
        }

        return null;
    }
}
