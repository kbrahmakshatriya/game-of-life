using System.Diagnostics.CodeAnalysis;

using GameOfLife.Core;

namespace GameOfLife.Api.Boards;

/// <summary>
/// Converts between <see cref="Board"/> and the API's cell matrix: a list of rows, top row first, each a list of cells
/// with 1 for live and 0 for dead.
/// </summary>
public static class CellMatrix
{
    /// <summary>Returns the cells of <paramref name="board"/> as a matrix of 1s and 0s.</summary>
    public static int[][] FromBoard(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var cells = new int[board.Rows][];
        for (int row = 0; row < board.Rows; row++)
        {
            cells[row] = new int[board.Columns];
            for (int column = 0; column < board.Columns; column++)
            {
                cells[row][column] = board[row, column] ? 1 : 0;
            }
        }

        return cells;
    }

    /// <summary>
    /// Creates a board from <paramref name="cells"/> when it is a rectangular matrix of 0s and 1s within the limits.
    /// </summary>
    /// <param name="cells">The matrix as it arrived in a request, so it may be null or contain null rows.</param>
    /// <param name="maxRows">The most rows allowed.</param>
    /// <param name="maxColumns">The most columns allowed.</param>
    /// <param name="board">The board, when the matrix is valid.</param>
    /// <param name="error">The first problem found, when it is not.</param>
    public static bool TryCreateBoard(
        int[][]? cells, int maxRows, int maxColumns,
        [NotNullWhen(true)] out Board? board, [NotNullWhen(false)] out FieldError? error)
    {
        error = FindError(cells, maxRows, maxColumns);
        if (error is not null)
        {
            board = null;
            return false;
        }

        // FindError found nothing wrong, so the matrix is non-null and rectangular.
        int[][] valid = cells!;
        board = Board.Create(valid.Length, valid[0].Length, (row, column) => valid[row][column] == 1);
        return true;
    }

    // Checks from the outside in, so that each check can rely on the ones before it, and stops at the first problem.
    // Listing every bad cell of a large board would make the error response as large as the board.
    private static FieldError? FindError(int[][]? cells, int maxRows, int maxColumns)
    {
        if (cells is null)
        {
            return new("cells", "The cells are required: a list of rows, each a list of 0 (dead) and 1 (live) cells.");
        }

        if (cells.Length == 0 || cells.Length > maxRows)
        {
            return new("cells", $"A board needs 1 to {maxRows} rows; this one has {cells.Length}.");
        }

        // JSON can contain null rows even though the C# type says a row is never null.
        int nullRow = Array.FindIndex(cells, row => row is null);
        if (nullRow >= 0)
        {
            return new($"cells[{nullRow}]", "A row must be a list of cells, not null.");
        }

        int columns = cells[0].Length;
        if (columns == 0 || columns > maxColumns)
        {
            return new("cells[0]", $"A row needs 1 to {maxColumns} cells; the first row has {columns}.");
        }

        for (int row = 1; row < cells.Length; row++)
        {
            if (cells[row].Length != columns)
            {
                return new(
                    $"cells[{row}]",
                    $"Every row needs as many cells as the first row, which has {columns}; this one has {cells[row].Length}.");
            }
        }

        for (int row = 0; row < cells.Length; row++)
        {
            int column = cells[row].AsSpan().IndexOfAnyExcept(0, 1);
            if (column >= 0)
            {
                return new(
                    $"cells[{row}][{column}]",
                    $"A cell must be 0 (dead) or 1 (live); this one is {cells[row][column]}.");
            }
        }

        return null;
    }
}
