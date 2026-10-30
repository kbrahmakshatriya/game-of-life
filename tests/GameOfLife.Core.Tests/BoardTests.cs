namespace GameOfLife.Core.Tests;

public sealed class BoardTests
{
    [Fact]
    public void FromMatrix_KeepsTheCells()
    {
        Board board = Board.FromMatrix([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0]]);

        Assert.Equal(3, board.Rows);
        Assert.Equal(4, board.Columns);
        Assert.Equal(3, board.LiveCellCount);
        Assert.Equal("""
            O...
            .O..
            ..O.
            """, board.ToString());
    }

    [Fact]
    public void ToMatrix_WritesLiveCellsAsOneAndDeadCellsAsZero()
    {
        int[][] expected = [[1, 0, 0], [0, 1, 1]];

        Assert.Equal(expected, Board.Parse("O..\n.OO").ToMatrix());
    }

    [Fact]
    public void FromMatrix_ThenToMatrix_GivesBackEveryCell()
    {
        // A fixed seed gives the same cells on every run.
        var random = new Random(42);
        int[][] cells = Enumerable.Range(0, 13)
            .Select(_ => Enumerable.Range(0, 17).Select(_ => random.Next(2)).ToArray())
            .ToArray();

        Board board = Board.FromMatrix(cells);

        Assert.Equal(cells, board.ToMatrix());
        Assert.Equal(cells.Sum(row => row.Sum()), board.LiveCellCount);
        for (int row = 0; row < board.Rows; row++)
        {
            for (int column = 0; column < board.Columns; column++)
            {
                Assert.Equal(cells[row][column] == 1, board[row, column]);
            }
        }
    }

    [Fact]
    public void FromMatrix_KeepsACopy()
    {
        int[][] cells = [[1, 0], [0, 1]];
        Board board = Board.FromMatrix(cells);

        cells[0][1] = 1;

        Assert.Equal("O.\n.O", board.ToString());
    }

    [Fact]
    public void ToMatrix_ReturnsACopy()
    {
        Board board = Board.Parse("O.\n.O");

        board.ToMatrix()[0][1] = 1;

        Assert.Equal("O.\n.O", board.ToString());
    }

    public static TheoryData<int[][], string> MalformedMatrices => new()
    {
        { [], "at least one row" },
        { [[]], "at least one column" },
        { [null!], "Row 0 is null" },
        { [[0], null!], "Row 1 is null" },
        { [[0, 1], [1]], "which has 2; row 1 has 1" },
        { [[0, 1], [1, 2]], "Row 1, column 1 is 2" },
        { [[-1]], "Row 0, column 0 is -1" },
    };

    [Theory]
    [MemberData(nameof(MalformedMatrices))]
    public void FromMatrix_RejectsMalformedMatrices(int[][] cells, string problem)
    {
        var exception = Assert.Throws<ArgumentException>(() => Board.FromMatrix(cells));

        Assert.Contains(problem, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromMatrix_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => Board.FromMatrix(null!));

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
        Board copy = Board.FromMatrix(board.ToMatrix());

        Assert.Equal(board, copy);
        Assert.True(board == copy);
        Assert.False(board != copy);
        Assert.Equal(board.GetHashCode(), copy.GetHashCode());
    }

    [Theory]
    [InlineData("OO\nOO", "OO\nO.")] // one cell differs
    [InlineData("OO..", "OO\n..")] // the same cells in the same order, in a different shape
    [InlineData("OO", "OO\n..")] // the same cells and columns, with an extra row
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
