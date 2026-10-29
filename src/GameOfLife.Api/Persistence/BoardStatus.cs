namespace GameOfLife.Api.Persistence;

/// <summary>Where a stored board is in its life. Settled boards use the names of <c>FinalStateKind</c>.</summary>
public enum BoardStatus
{
    /// <summary>The board is still evolving, so next can advance it.</summary>
    Active,

    /// <summary>Settled: every cell is dead.</summary>
    Extinct,

    /// <summary>Settled: live cells remain, and the board no longer changes.</summary>
    StillLife,

    /// <summary>Settled: the board repeats a cycle of two or more generations forever.</summary>
    Oscillator,
}
