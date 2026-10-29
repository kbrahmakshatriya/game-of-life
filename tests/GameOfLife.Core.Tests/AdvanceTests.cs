namespace GameOfLife.Core.Tests;

public sealed class AdvanceTests
{
    [Fact]
    public void ZeroGenerations_ReturnsTheStartingBoard()
    {
        Board board = Board.Parse(Patterns.Glider);

        Assert.Same(board, Simulation.Advance(board, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17)]
    public void NGenerations_IsNextGenerationAppliedNTimes(int generations)
    {
        Board board = Patterns.Place(Patterns.RPentomino, 16, 16, 6, 6);
        Board expected = board;
        for (int generation = 0; generation < generations; generation++)
        {
            expected = expected.NextGeneration();
        }

        Assert.Equal(expected, Simulation.Advance(board, generations));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(999, false)]
    [InlineData(1_000, true)]
    public void Blinker_IsInItsStartingPhaseAfterEveryEvenNumberOfGenerations(int generations, bool isStartingPhase)
    {
        Board blinker = Patterns.Pad(Patterns.Blinker, 1);

        Assert.Equal(isStartingPhase, Simulation.Advance(blinker, generations) == blinker);
    }

    [Fact]
    public void Glider_OnATenByTenBoard_EndsAsABlockInTheOppositeCorner()
    {
        Board glider = Patterns.Place(Patterns.Glider, 10, 10, 0, 0);

        Assert.Equal(Patterns.Place(Patterns.Block, 10, 10, 8, 8), Simulation.Advance(glider, 31));
    }

    [Fact]
    public void NegativeGenerations_Throws()
    {
        Board board = Board.Parse(Patterns.Glider);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.Advance(board, -1));
        Assert.Equal("generations", exception.ParamName);
    }

    [Fact]
    public void NullBoard_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Simulation.Advance(null!, 1));

    [Fact]
    public void CancelledToken_StopsTheRun()
    {
        Board board = Board.Parse(Patterns.Glider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => Simulation.Advance(board, 1, cancellation.Token));
    }
}
