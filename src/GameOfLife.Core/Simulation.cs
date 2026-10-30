using System.Security.Cryptography;
using System.Text;

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
    /// Only a fingerprint of each generation is kept: a SHA-256 hash of its cells, 64 characters however large the
    /// board. Equal boards always have equal fingerprints, so a repeat is never missed. Two different boards could
    /// only be mistaken for a repeat if their hashes collided, which is too unlikely to matter.
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

        // The fingerprint of every generation seen so far, with the generation number at which it first appeared.
        var firstSeenAt = new Dictionary<string, int> { [Fingerprint(board)] = 0 };
        Board current = board;

        // Counting steps from 0 keeps the loop from overflowing when maxGenerations is int.MaxValue.
        for (int step = 0; step < maxGenerations; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = current.NextGeneration();
            int generation = step + 1;

            string fingerprint = Fingerprint(current);
            if (firstSeenAt.TryGetValue(fingerprint, out int cycleStart))
            {
                return new FinalState(current, cycleStart, generation - cycleStart);
            }

            firstSeenAt.Add(fingerprint, generation);
        }

        return null;
    }

    // A SHA-256 hash of the board's text. Board.Parse reads that text back into the same board, so different boards
    // have different text, and so different fingerprints unless SHA-256 collides.
    private static string Fingerprint(Board board) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board.ToString())));
}
