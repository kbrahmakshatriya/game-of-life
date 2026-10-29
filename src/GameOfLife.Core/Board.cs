using System.Numerics;
using System.Runtime.InteropServices;
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
/// Two boards are equal when they have the same dimensions and the same live cells. Cells are stored as bits, 64 to a
/// <see cref="ulong"/>, so a board takes one bit per cell, and comparing or hashing boards handles 64 cells at a time.
/// <see cref="Simulation.FindFinalState"/> relies on both: it keeps every generation it computes and looks each new one
/// up among them.
/// </para>
/// </remarks>
public sealed class Board : IEquatable<Board>
{
    /// <summary>The character that <see cref="Parse"/> reads and <see cref="ToString"/> writes for a live cell.</summary>
    public const char LiveCell = 'O';

    /// <summary>The character that <see cref="Parse"/> reads and <see cref="ToString"/> writes for a dead cell.</summary>
    public const char DeadCell = '.';

    private const int CellsPerWord = 64;

    // Cell (row, column) is bit number (row * Columns + column), counting across the words in order. Bits past the
    // last cell are always 0, so equal boards have equal words.
    private readonly ulong[] _words;

    // Cached because a board never changes and FindFinalState hashes every generation. HashCode is seeded randomly
    // per process, so this value must never be stored or sent anywhere.
    private readonly int _hashCode;

    private Board(int rows, int columns, ulong[] words)
    {
        Rows = rows;
        Columns = columns;
        _words = words;
        LiveCellCount = CountLiveCells(words);
        _hashCode = ComputeHashCode(rows, columns, words);
    }

    /// <summary>Gets the number of rows.</summary>
    public int Rows { get; }

    /// <summary>Gets the number of columns.</summary>
    public int Columns { get; }

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
    /// Creates a board of <paramref name="rows"/> by <paramref name="columns"/> cells, calling
    /// <paramref name="isAlive"/> once for each cell, row by row, to ask whether it is alive.
    /// </summary>
    /// <param name="rows">The number of rows, at least 1.</param>
    /// <param name="columns">The number of columns, at least 1.</param>
    /// <param name="isAlive">Given a row and a column, returns whether that cell is alive.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1.</exception>
    /// <exception cref="ArgumentException">The board would have more than <see cref="int.MaxValue"/> cells.</exception>
    public static Board Create(int rows, int columns, Func<int, int, bool> isAlive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentNullException.ThrowIfNull(isAlive);
        if ((long)rows * columns > int.MaxValue)
        {
            throw new ArgumentException(
                $"A board can have at most {int.MaxValue} cells; {rows} rows of {columns} columns is more.");
        }

        var words = new ulong[WordCount(rows * columns)];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (isAlive(row, column))
                {
                    SetAlive(words, (row * columns) + column);
                }
            }
        }

        return new Board(rows, columns, words);
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

        return Create(lines.Length, columns, (row, column) => lines[row][column] == LiveCell);
    }

    /// <summary>
    /// Returns the next generation. Conway's rules apply to every cell at once: a live cell with two or three live
    /// neighbors survives, a dead cell with exactly three live neighbors comes alive, and every other cell is dead.
    /// </summary>
    /// <remarks>This board is not changed; the next generation is a new board of the same size.</remarks>
    public Board NextGeneration()
    {
        var next = new ulong[_words.Length];
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (WillBeAlive(IsAlive(row, column), CountLiveNeighbors(row, column)))
                {
                    SetAlive(next, (row * Columns) + column);
                }
            }
        }

        return new Board(Rows, Columns, next);
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
            && _hashCode == other._hashCode
            && Rows == other.Rows
            && Columns == other.Columns
            && _words.AsSpan().SequenceEqual(other._words));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Board);

    /// <inheritdoc />
    public override int GetHashCode() => _hashCode;

    /// <summary>Returns whether two boards have the same dimensions and the same live cells.</summary>
    public static bool operator ==(Board? left, Board? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Returns whether two boards differ in their dimensions or their live cells.</summary>
    public static bool operator !=(Board? left, Board? right) => !(left == right);

    // Conway's rule, written B3/S23: a cell is born with exactly 3 live neighbors and survives with 2 or 3.
    private static bool WillBeAlive(bool isAlive, int liveNeighbors) =>
        liveNeighbors == 3 || (isAlive && liveNeighbors == 2);

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

    private bool IsAlive(int row, int column) => IsAlive(_words, (row * Columns) + column);

    private static bool IsAlive(ulong[] words, int cell) => (words[cell / CellsPerWord] & Bit(cell)) != 0;

    private static void SetAlive(ulong[] words, int cell) => words[cell / CellsPerWord] |= Bit(cell);

    private static ulong Bit(int cell) => 1UL << (cell % CellsPerWord);

    private static int WordCount(int cells) => (cells / CellsPerWord) + (cells % CellsPerWord == 0 ? 0 : 1);

    private static int CountLiveCells(ulong[] words)
    {
        int count = 0;
        foreach (ulong word in words)
        {
            count += BitOperations.PopCount(word);
        }

        return count;
    }

    private static int ComputeHashCode(int rows, int columns, ulong[] words)
    {
        var hash = new HashCode();
        hash.Add(rows);
        hash.Add(columns);
        hash.AddBytes(MemoryMarshal.AsBytes(words.AsSpan()));
        return hash.ToHashCode();
    }
}
