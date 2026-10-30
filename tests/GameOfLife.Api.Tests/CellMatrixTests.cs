using GameOfLife.Api.Boards;
using GameOfLife.Core;

namespace GameOfLife.Api.Tests;

public sealed class CellMatrixTests
{
    private const int MaxRows = 4;
    private const int MaxColumns = 5;

    [Fact]
    public void TryCreateBoard_ValidMatrix_CreatesTheBoard()
    {
        int[][] cells = [[1, 0, 0], [0, 1, 1]];

        Assert.True(CellMatrix.TryCreateBoard(cells, MaxRows, MaxColumns, out Board? board, out FieldError? error));
        Assert.Null(error);
        Assert.Equal(Board.Parse("O..\n.OO"), board);
    }

    [Fact]
    public void TryCreateBoard_MatrixExactlyAtTheLimits_IsValid()
    {
        int[][] cells = Enumerable.Range(0, MaxRows).Select(_ => new int[MaxColumns]).ToArray();

        Assert.True(CellMatrix.TryCreateBoard(cells, MaxRows, MaxColumns, out Board? board, out _));
        Assert.Equal(MaxRows, board.Rows);
        Assert.Equal(MaxColumns, board.Columns);
    }

    public static TheoryData<int[][]?, string, string> InvalidMatrices => new()
    {
        { null, "cells", "required" },
        { [], "cells", "1 to 4 rows; this one has 0" },
        { [[0], [0], [0], [0], [0]], "cells", "1 to 4 rows; this one has 5" },
        { [[0], null!], "cells[1]", "not null" },
        { [[]], "cells[0]", "1 to 5 cells; the first row has 0" },
        { [[0, 0, 0, 0, 0, 0]], "cells[0]", "1 to 5 cells; the first row has 6" },
        { [[0, 1], [1]], "cells[1]", "which has 2; this one has 1" },
        { [[0, 1], [1, 2]], "cells[1][1]", "this one is 2" },
        { [[-1]], "cells[0][0]", "this one is -1" },
    };

    [Theory]
    [MemberData(nameof(InvalidMatrices))]
    public void TryCreateBoard_InvalidMatrix_NamesTheFieldAndTheProblem(int[][]? cells, string field, string problem)
    {
        Assert.False(CellMatrix.TryCreateBoard(cells, MaxRows, MaxColumns, out Board? board, out FieldError? error));

        Assert.Null(board);
        Assert.NotNull(error);
        Assert.Equal(field, error.Field);
        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }
}
