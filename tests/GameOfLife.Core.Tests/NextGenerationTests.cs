namespace GameOfLife.Core.Tests;

public sealed class NextGenerationTests
{
    // The eight neighbors of the center of a 3x3 board, in the order the rule table below brings them to life.
    private static readonly (int Row, int Column)[] CenterNeighbors =
        [(0, 0), (0, 1), (0, 2), (1, 0), (1, 2), (2, 0), (2, 1), (2, 2)];

    [Theory]
    // A live cell with fewer than two live neighbors dies, as if by underpopulation...
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    // ...with two or three it lives on...
    [InlineData(true, 2, true)]
    [InlineData(true, 3, true)]
    // ...and with more than three it dies, as if by overpopulation.
    [InlineData(true, 4, false)]
    [InlineData(true, 5, false)]
    [InlineData(true, 6, false)]
    [InlineData(true, 7, false)]
    [InlineData(true, 8, false)]
    // A dead cell with exactly three live neighbors becomes alive, as if by reproduction; otherwise it stays dead.
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(false, 3, true)]
    [InlineData(false, 4, false)]
    [InlineData(false, 5, false)]
    [InlineData(false, 6, false)]
    [InlineData(false, 7, false)]
    [InlineData(false, 8, false)]
    public void CenterCell_FollowsConwaysRules(bool isAlive, int liveNeighbors, bool isAliveNext)
    {
        Board board = Board.Create(3, 3, (row, column) =>
            (row, column) == (1, 1) ? isAlive : CenterNeighbors.Take(liveNeighbors).Contains((row, column)));

        Assert.Equal(isAliveNext, board.NextGeneration()[1, 1]);
    }

    [Theory]
    [InlineData(Patterns.Block)]
    [InlineData(Patterns.Beehive)]
    [InlineData(Patterns.Loaf)]
    [InlineData(Patterns.Boat)]
    [InlineData(Patterns.Tub)]
    public void StillLifes_DoNotChange(string pattern)
    {
        Board board = Patterns.Pad(pattern, 1);

        Assert.Equal(board, board.NextGeneration());
    }

    [Theory]
    [InlineData(Patterns.Blinker, 2)]
    [InlineData(Patterns.Toad, 2)]
    [InlineData(Patterns.Beacon, 2)]
    [InlineData(Patterns.Pulsar, 3)]
    public void Oscillators_ReturnToTheirFirstPhaseAfterExactlyTheirPeriod(string pattern, int period)
    {
        Board start = Patterns.Pad(pattern, 2);

        Board current = start;
        for (int generation = 1; generation < period; generation++)
        {
            current = current.NextGeneration();
            Assert.NotEqual(start, current);
        }

        Assert.Equal(start, current.NextGeneration());
    }

    [Fact]
    public void Blinker_TurnsBetweenHorizontalAndVertical()
    {
        Board horizontal = Board.Parse("""
            ...
            OOO
            ...
            """);
        Board vertical = Board.Parse("""
            .O.
            .O.
            .O.
            """);

        Assert.Equal(vertical, horizontal.NextGeneration());
        Assert.Equal(horizontal, vertical.NextGeneration());
    }

    [Fact]
    public void Glider_MovesOneCellDiagonallyEveryFourGenerations()
    {
        Board glider = Patterns.Place(Patterns.Glider, 8, 8, 0, 0);

        Board later = glider.NextGeneration().NextGeneration().NextGeneration().NextGeneration();

        Assert.Equal(Patterns.Place(Patterns.Glider, 8, 8, 1, 1), later);
    }

    [Fact]
    public void CellsBeyondTheEdges_CountAsDead()
    {
        // A blinker against the left edge. Unbounded, it would turn horizontal across columns -1 to 1, but column -1
        // does not exist, so only columns 0 and 1 of the middle row come alive.
        Board board = Board.Parse("""
            O..
            O..
            O..
            """);

        Assert.Equal(Board.Parse("""
            ...
            OO.
            ...
            """), board.NextGeneration());
    }

    [Theory]
    [InlineData("OOO", ".O.")]
    [InlineData("O\nO\nO", ".\nO\n.")]
    [InlineData("O", ".")]
    public void BoardsOneCellWide_LoseTheirEndsAndThenDie(string pattern, string expected) =>
        Assert.Equal(Board.Parse(expected), Board.Parse(pattern).NextGeneration());

    [Fact]
    public void EmptyBoard_StaysEmpty()
    {
        Board empty = Board.Create(4, 5, (_, _) => false);

        Assert.Equal(empty, empty.NextGeneration());
    }

    [Fact]
    public void NextGeneration_LeavesTheBoardItWasCalledOnUnchanged()
    {
        Board board = Patterns.Pad(Patterns.Blinker, 1);
        string before = board.ToString();

        _ = board.NextGeneration();

        Assert.Equal(before, board.ToString());
    }
}
