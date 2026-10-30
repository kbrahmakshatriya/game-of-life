namespace GameOfLife.Core.Tests;

public sealed class FindFinalStateTests
{
    // More than any board below needs, so each of them settles within it.
    private const int Limit = 2_000;

    private static readonly Dictionary<string, Board> Examples = new()
    {
        ["Empty"] = Patterns.Create(3, 3, (_, _) => false),
        ["SingleCell"] = Patterns.Pad("O", 1),
        ["FullThreeByThree"] = Board.Parse("OOO\nOOO\nOOO"),
        ["Diehard"] = Patterns.Place(Patterns.Diehard, 40, 40, 18, 16),
        ["Block"] = Patterns.Pad(Patterns.Block, 1),
        ["GliderOnTenByTen"] = Patterns.Place(Patterns.Glider, 10, 10, 0, 0),
        ["Blinker"] = Patterns.Pad(Patterns.Blinker, 1),
        ["Toad"] = Patterns.Pad(Patterns.Toad, 1),
        ["Pulsar"] = Patterns.Pad(Patterns.Pulsar, 2),
        // A row of ten cells grows into a pentadecathlon, which has a period of 15.
        ["RowOfTen"] = Patterns.Place("OOOOOOOOOO", 11, 18, 5, 4),
        ["RPentominoOn68By68"] = Patterns.Place(Patterns.RPentomino, 68, 68, 32, 32),
    };

    [Theory]
    [InlineData("Empty", 0, 1, FinalStateKind.Extinct)]
    [InlineData("SingleCell", 1, 1, FinalStateKind.Extinct)]
    [InlineData("FullThreeByThree", 2, 1, FinalStateKind.Extinct)]
    [InlineData("Diehard", 130, 1, FinalStateKind.Extinct)]
    [InlineData("Block", 0, 1, FinalStateKind.StillLife)]
    [InlineData("GliderOnTenByTen", 31, 1, FinalStateKind.StillLife)]
    [InlineData("Blinker", 0, 2, FinalStateKind.Oscillator)]
    [InlineData("Toad", 0, 2, FinalStateKind.Oscillator)]
    [InlineData("Pulsar", 0, 3, FinalStateKind.Oscillator)]
    [InlineData("RowOfTen", 2, 15, FinalStateKind.Oscillator)]
    [InlineData("RPentominoOn68By68", 1_163, 2, FinalStateKind.Oscillator)]
    public void KnownBoards_SettleAtTheExpectedGenerationWithTheExpectedPeriod(
        string example, int generation, int period, FinalStateKind kind)
    {
        Board board = Examples[example];

        FinalState? result = Simulation.FindFinalState(board, Limit);

        Assert.NotNull(result);
        Assert.Equal(generation, result.Generation);
        Assert.Equal(period, result.Period);
        Assert.Equal(kind, result.Kind);
        // The final state is the board at that generation, and it comes back after one period.
        Assert.Equal(Simulation.Advance(board, generation), result.Board);
        Assert.Equal(result.Board, Simulation.Advance(result.Board, period));
    }

    [Fact]
    public void Limit_IsTheNumberOfGenerationsComputed()
    {
        // The glider becomes a block at generation 31, but the block is only seen to be still when generation 32
        // repeats it, so a limit of 31 is one too few.
        Board glider = Examples["GliderOnTenByTen"];

        Assert.Null(Simulation.FindFinalState(glider, 31));
        Assert.NotNull(Simulation.FindFinalState(glider, 32));
    }

    [Fact]
    public void RPentomino_DoesNotSettleWithinAThousandGenerations() =>
        Assert.Null(Simulation.FindFinalState(Examples["RPentominoOn68By68"], 1_000));

    [Fact]
    public void Results_MatchASimpleSearchOnRandomBoards()
    {
        // A fixed seed gives the same boards on every run.
        var random = new Random(42);
        const int BoardCount = 300;
        int unsettled = 0;

        for (int i = 0; i < BoardCount; i++)
        {
            Board board = Patterns.Create(random.Next(1, 8), random.Next(1, 8), (_, _) => random.Next(2) == 1);
            // Low limits stop some searches before they finish, so both outcomes get compared.
            int maxGenerations = random.Next(1, 40);

            FinalState? expected = SimpleFindFinalState(board, maxGenerations);

            Assert.Equal(expected, Simulation.FindFinalState(board, maxGenerations));
            unsettled += expected is null ? 1 : 0;
        }

        Assert.InRange(unsettled, 1, BoardCount - 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LimitBelowOne_Throws(int maxGenerations)
    {
        Board board = Board.Parse("O");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Simulation.FindFinalState(board, maxGenerations));
        Assert.Equal("maxGenerations", exception.ParamName);
    }

    [Fact]
    public void NullBoard_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Simulation.FindFinalState(null!, Limit));

    [Fact]
    public void CancelledToken_StopsTheSearch()
    {
        Board board = Examples["Blinker"];
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => Simulation.FindFinalState(board, Limit, cancellation.Token));
    }

    // The obvious method, written independently of the code under test: keep every generation's text in a list and
    // compare each new generation with all the earlier ones.
    private static FinalState? SimpleFindFinalState(Board board, int maxGenerations)
    {
        var history = new List<string> { board.ToString() };
        Board current = board;
        for (int generation = 1; generation <= maxGenerations; generation++)
        {
            current = current.NextGeneration();
            int earlier = history.IndexOf(current.ToString());
            if (earlier >= 0)
            {
                return new FinalState(current, earlier, generation - earlier);
            }

            history.Add(current.ToString());
        }

        return null;
    }
}
