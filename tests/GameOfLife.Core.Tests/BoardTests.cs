namespace GameOfLife.Core.Tests;

public sealed class BoardTests
{
    [Fact]
    public void Create_AsksForEveryCellAndKeepsTheAnswers()
    {
        Board board = Board.Create(3, 4, (row, column) => row == column);

        Assert.Equal(3, board.Rows);
        Assert.Equal(4, board.Columns);
        Assert.Equal(3, board.LiveCellCount);
        Assert.Equal("""
            O...
            .O..
            ..O.
            """, board.ToString());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 8)] // exactly one 64-bit word
    [InlineData(1, 65)] // one cell into a second word
    [InlineData(13, 17)] // rows that start part-way through a word
    [InlineData(64, 64)]
    public void Create_KeepsEveryCellOfBoardsSpanningManyWords(int rows, int columns)
    {
        // A fixed seed gives the same cells on every run.
        var random = new Random(rows * 1_000 + columns);
        bool[][] cells = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, columns).Select(_ => random.Next(2) == 1).ToArray())
            .ToArray();

        Board board = Board.Create(rows, columns, (row, column) => cells[row][column]);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Assert.Equal(cells[row][column], board[row, column]);
            }
        }

        Assert.Equal(cells.Sum(row => row.Count(isAlive => isAlive)), board.LiveCellCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    public void Create_RejectsDimensionsBelowOne(int rows, int columns) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.Create(rows, columns, (_, _) => false));

    [Fact]
    public void Create_RejectsMoreCellsThanAnIntCanCount() =>
        // 2^32 cells. The size is checked before anything is allocated.
        Assert.Throws<ArgumentException>(() => Board.Create(65_536, 65_536, (_, _) => false));

    [Fact]
    public void Create_RejectsANullDelegate() =>
        Assert.Throws<ArgumentNullException>(() => Board.Create(1, 1, null!));

    [Theory]
    [InlineData(-1, 0, "row")]
    [InlineData(2, 0, "row")]
    [InlineData(0, -1, "column")]
    [InlineData(0, 3, "column")]
    public void Indexer_OutsideTheBoard_Throws(int row, int column, string parameter)
    {
        Board board = Board.Parse("""
            O.O
            .OO
            """);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => board[row, column]);
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    [InlineData("O")]
    [InlineData(".")]
    [InlineData("O.O\n.OO")]
    [InlineData("O\n.\nO")]
    [InlineData(Patterns.Pulsar)]
    public void ToString_WritesWhatParseReads(string text) =>
        Assert.Equal(text, Board.Parse(text).ToString());

    [Fact]
    public void Parse_AcceptsWindowsLineEndingsAndTrailingLineBreaks() =>
        Assert.Equal(Board.Parse("O.\n.O"), Board.Parse("O.\r\n.O\r\n\n"));

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("O.\nO")] // rows of different lengths
    [InlineData("O\n\nO")] // an empty row
    [InlineData("O0")] // the digit zero, not the letter O
    [InlineData(".#.")]
    [InlineData(" O")]
    public void Parse_RejectsMalformedText(string text) =>
        Assert.Throws<FormatException>(() => Board.Parse(text));

    [Fact]
    public void Boards_WithTheSameDimensionsAndCells_AreEqual()
    {
        Board board = Board.Parse(Patterns.Glider);
        Board copy = Board.Create(board.Rows, board.Columns, (row, column) => board[row, column]);

        Assert.Equal(board, copy);
        Assert.True(board == copy);
        Assert.False(board != copy);
        Assert.Equal(board.GetHashCode(), copy.GetHashCode());
    }

    [Theory]
    [InlineData("OO\nOO", "OO\nO.")] // one cell differs
    [InlineData("OO..", "OO\n..")] // the same bits in the same order, in a different shape
    [InlineData("OO", "OO\n..")] // the same bits and columns, with an extra row
    [InlineData("....", "..\n..")] // empty boards of different shapes
    public void Boards_WithDifferentCellsOrDimensions_AreNotEqual(string text, string otherText)
    {
        Board board = Board.Parse(text);
        Board other = Board.Parse(otherText);

        Assert.NotEqual(board, other);
        Assert.False(board == other);
        Assert.True(board != other);
    }

    [Fact]
    public void Board_IsNotEqualToNullOrToOtherTypes()
    {
        Board board = Board.Parse("O");

        Assert.False(board.Equals(null));
        Assert.False(board.Equals((object)"O"));
        Assert.False(board == null);
        Assert.False(null == board);
    }
}
