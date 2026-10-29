namespace GameOfLife.Core;

/// <summary>
/// Where a board's evolution settles: from generation <paramref name="Generation"/> on, it repeats the same
/// <paramref name="Period"/> generations forever, starting with <paramref name="Board"/>.
/// </summary>
/// <param name="Board">The board at <paramref name="Generation"/>, the first generation of the repeating cycle.</param>
/// <param name="Generation">
/// The generation the cycle starts at, counting the starting board as generation 0. It is 0 when the starting board is
/// already part of the cycle.
/// </param>
/// <param name="Period">
/// How many generations one cycle takes: 1 for a board that no longer changes, more for an oscillator.
/// </param>
public sealed record FinalState(Board Board, int Generation, int Period)
{
    /// <summary>Gets how the board settled.</summary>
    public FinalStateKind Kind => (Period, Board.LiveCellCount) switch
    {
        (1, 0) => FinalStateKind.Extinct,
        (1, _) => FinalStateKind.StillLife,
        _ => FinalStateKind.Oscillator,
    };
}
