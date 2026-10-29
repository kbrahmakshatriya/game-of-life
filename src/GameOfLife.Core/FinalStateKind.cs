namespace GameOfLife.Core;

/// <summary>The ways a board can settle, named as in the Game of Life literature.</summary>
public enum FinalStateKind
{
    /// <summary>Every cell is dead, so no cell can ever come alive again.</summary>
    Extinct,

    /// <summary>Some cells are alive and the board no longer changes: a period of 1.</summary>
    StillLife,

    /// <summary>The board repeats a cycle of two or more different generations forever.</summary>
    Oscillator,
}
