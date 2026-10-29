using System.Text.Json.Serialization;

using GameOfLife.Api.Persistence;
using GameOfLife.Core;

namespace GameOfLife.Api.Boards;

/// <summary>A board to upload.</summary>
/// <param name="Cells">
/// The grid as a list of rows, top row first. Each row is a list of cells, 1 for live and 0 for dead, and every row
/// has the same number of cells.
/// </param>
public sealed record UploadBoardRequest(int[][]? Cells);

/// <summary>A stored board at its current generation.</summary>
/// <param name="Id">The board's id.</param>
/// <param name="Generation">The board's current generation. The board as uploaded is generation 0.</param>
/// <param name="Status">
/// Active while the board can still advance. Once its final state is saved: Extinct when every cell is dead, StillLife
/// when live cells remain but nothing changes, or Oscillator when it repeats a cycle of two or more generations.
/// </param>
/// <param name="Period">
/// For a settled board, how many generations its cycle takes: 1 when it no longer changes. Absent while it is active.
/// </param>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="LiveCells">How many cells are alive.</param>
/// <param name="Cells">The grid as a list of rows, top row first, with 1 for live and 0 for dead.</param>
public sealed record BoardResponse(
    Guid Id,
    long Generation,
    BoardStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Period,
    int Rows,
    int Columns,
    int LiveCells,
    int[][] Cells)
{
    public static BoardResponse From(StoredBoard stored) => new(
        stored.Id, stored.Generation, stored.Status, stored.Period,
        stored.Board.Rows, stored.Board.Columns, stored.Board.LiveCellCount, CellMatrix.FromBoard(stored.Board));
}

/// <summary>A generation computed ahead of a board's current one. Nothing about the board is saved.</summary>
/// <param name="Id">The board's id.</param>
/// <param name="Generation">The generation shown: the board's current generation plus n.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="LiveCells">How many cells are alive.</param>
/// <param name="Cells">The grid as a list of rows, top row first, with 1 for live and 0 for dead.</param>
public sealed record PreviewResponse(Guid Id, long Generation, int Rows, int Columns, int LiveCells, int[][] Cells)
{
    public static PreviewResponse From(Guid id, long generation, Board board) =>
        new(id, generation, board.Rows, board.Columns, board.LiveCellCount, CellMatrix.FromBoard(board));
}
