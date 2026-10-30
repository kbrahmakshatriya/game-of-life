using System.Text;

namespace GameOfLife.Core;

/// <summary>
/// One generation of a Game of Life: an immutable, finite grid of live and dead cells.
/// </summary>
/// <remarks>
/// <para>
/// The grid does not wrap around. Positions beyond its edges count as permanently dead, so a cell on an edge has
/// fewer neighbors than one in the middle.
/// </para>
/// <para>
/// The cells are kept as a matrix: a list of rows, top row first, each a list of cells with 1 for live and 0 for dead.
/// <see cref="FromMatrix"/> and <see cref="ToMatrix"/> copy it on the way in and out, so nothing outside the board can
/// change its cells. Two boards are equal when they have the same dimensions and the same live cells.
/// </para>
/// </remarks>
public sealed class Board : IEquatable<Board>
{
    /// <summary>The character that <see cref="Parse"/> reads and <see cref="ToString"/> writes for a live cell.</summary>
    public const char LiveCell = 'O';

    /// <summary>The character that <see cref="Parse"/> reads and <see cref="ToString"/> writes for a dead cell.</summary>
    public const char DeadCell = '.';

    // Indexed [row][column]. Every row has the same length, and no row is changed once the board has it.
    private readonly int[][] _cells;

    private Board(int[][] cells)
    {
        _cells = cells;
        LiveCellCount = cells.Sum(row => row.Sum());
    }

    /// <summary>Gets the number of rows.</summary>
    public int Rows => _cells.Length;

    /// <summary>Gets the number of columns.</summary>
    public int Columns => _cells[0].Length;

    /// <summary>Gets the number of live cells.</summary>
    public int LiveCellCount { get; }

    /// <summary>Gets whether the cell at <paramref name="row"/> and <paramref name="column"/> is alive.</summary>
    /// <param name="row">The zero-based row, counted from the top.</param>
    /// <param name="column">The zero-based column, counted from the left.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the board.</exception>
    public bool this[int row, int column]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);

            return IsAlive(row, column);
        }
    }

    /// <summary>
    /// Creates a board from <paramref name="cells"/>: a list of rows, top row first, each a list of cells with 1 for
    /// live and 0 for dead.
    /// </summary>
    /// <remarks>The board keeps a copy, so changing <paramref name="cells"/> afterwards does not change the board.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The matrix has no rows or no columns, has a null row or rows of different lengths, or contains a value other
    /// than 0 or 1.
    /// </exception>
    public static Board FromMatrix(int[][] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length == 0)
        {
            throw new ArgumentException("A board needs at least one row.", nameof(cells));
        }

        for (int row = 0; row < cells.Length; row++)
        {
            // The type says a row is never null, but a caller that ignores nullable warnings can still pass one.
            if (cells[row] is null)
            {
                throw new ArgumentException($"Row {row} is null.", nameof(cells));
            }

            if (cells[row].Length != cells[0].Length)
            {
                throw new ArgumentException(
                    $"Every row needs as many cells as the first, which has {cells[0].Length}; row {row} has {cells[row].Length}.",
                    nameof(cells));
            }

            int invalid = cells[row].AsSpan().IndexOfAnyExcept(0, 1);
            if (invalid >= 0)
            {
                throw new ArgumentException(
                    $"Row {row}, column {invalid} is {cells[row][invalid]}; a cell must be 1 (live) or 0 (dead).",
                    nameof(cells));
            }
        }

        if (cells[0].Length == 0)
        {
            throw new ArgumentException("A board needs at least one column.", nameof(cells));
        }

        return new Board(Copy(cells));
    }

    /// <summary>
    /// Reads a board from text with one line per row, <see cref="LiveCell"/> for a live cell and
    /// <see cref="DeadCell"/> for a dead one: the format <see cref="ToString"/> writes, which is also LifeWiki's
    /// "plaintext" format without its comment lines.
    /// </summary>
    /// <remarks>Lines may be separated by <c>\n</c> or <c>\r\n</c>; line breaks at the end are ignored.</remarks>
    /// <exception cref="FormatException">
    /// The text has no cells, its rows differ in length, or it contains a character other than the two cell symbols.
    /// </exception>
    public static Board Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        int columns = lines[0].Length;
        if (columns == 0)
        {
            throw new FormatException("A board needs at least one row and one column.");
        }

        for (int row = 0; row < lines.Length; row++)
        {
            string line = lines[row];
            if (line.Length != columns)
            {
                throw new FormatException(
                    $"Every row needs as many cells as the first, which has {columns}; row {row} has {line.Length}.");
            }

            int invalid = line.AsSpan().IndexOfAnyExcept(LiveCell, DeadCell);
            if (invalid >= 0)
            {
                throw new FormatException(
                    $"Row {row}, column {invalid} is '{line[invalid]}'; a cell must be '{LiveCell}' (live) or '{DeadCell}' (dead).");
            }
        }

        return new Board(lines.Select(line => line.Select(cell => cell == LiveCell ? 1 : 0).ToArray()).ToArray());
    }

    /// <summary>
    /// Returns the cells as a matrix: a list of rows, top row first, each a list of cells with 1 for live and 0 for
    /// dead.
    /// </summary>
    /// <remarks>The matrix is a copy, so changing it does not change the board.</remarks>
    public int[][] ToMatrix() => Copy(_cells);

    /// <summary>
    /// Returns the next generation. Conway's rules apply to every cell at once: a live cell with two or three live
    /// neighbors survives, a dead cell with exactly three live neighbors comes alive, and every other cell is dead.
    /// </summary>
    /// <remarks>This board is not changed; the next generation is a new board of the same size.</remarks>
    public Board NextGeneration()
    {
        var next = new int[Rows][];
        for (int row = 0; row < Rows; row++)
        {
            next[row] = new int[Columns];
            for (int column = 0; column < Columns; column++)
            {
                next[row][column] = WillBeAlive(IsAlive(row, column), CountLiveNeighbors(row, column)) ? 1 : 0;
            }
        }

        return new Board(next);
    }

    /// <summary>
    /// Writes the board in the format <see cref="Parse"/> reads: one line per row, separated by <c>\n</c> on every
    /// platform, with no line break at the end.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        for (int row = 0; row < Rows; row++)
        {
            if (row > 0)
            {
                text.Append('\n');
            }

            for (int column = 0; column < Columns; column++)
            {
                text.Append(IsAlive(row, column) ? LiveCell : DeadCell);
            }
        }

        return text.ToString();
    }

    /// <summary>Returns whether <paramref name="other"/> has the same dimensions and the same live cells.</summary>
    public bool Equals(Board? other) =>
        ReferenceEquals(this, other)
        || (other is not null
            && Rows == other.Rows
            && Columns == other.Columns
            && _cells.Zip(other._cells).All(rows => rows.First.AsSpan().SequenceEqual(rows.Second)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Board);

    // Equal boards have the same dimensions and live cell count, so this is a valid hash code, if a weak one. Nothing
    // keeps boards in a large hash table: Simulation.FindFinalState keeps fingerprints instead.
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Rows, Columns, LiveCellCount);

    /// <summary>Returns whether two boards have the same dimensions and the same live cells.</summary>
    public static bool operator ==(Board? left, Board? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Returns whether two boards differ in their dimensions or their live cells.</summary>
    public static bool operator !=(Board? left, Board? right) => !(left == right);

    // Conway's rule, written B3/S23: a cell is born with exactly 3 live neighbors and survives with 2 or 3.
    private static bool WillBeAlive(bool isAlive, int liveNeighbors) =>
        liveNeighbors == 3 || (isAlive && liveNeighbors == 2);

    private static int[][] Copy(int[][] cells) => cells.Select(row => row.ToArray()).ToArray();

    // Counts the live cells among the up to eight that touch (row, column). Only positions on the board are visited,
    // which is what makes everything beyond the edges dead.
    private int CountLiveNeighbors(int row, int column)
    {
        int firstRow = Math.Max(row - 1, 0);
        int lastRow = Math.Min(row + 1, Rows - 1);
        int firstColumn = Math.Max(column - 1, 0);
        int lastColumn = Math.Min(column + 1, Columns - 1);

        int count = 0;
        for (int neighborRow = firstRow; neighborRow <= lastRow; neighborRow++)
        {
            for (int neighborColumn = firstColumn; neighborColumn <= lastColumn; neighborColumn++)
            {
                bool isTheCellItself = neighborRow == row && neighborColumn == column;
                if (!isTheCellItself && IsAlive(neighborRow, neighborColumn))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsAlive(int row, int column) => _cells[row][column] == 1;
}
