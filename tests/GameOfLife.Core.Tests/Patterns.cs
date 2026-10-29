namespace GameOfLife.Core.Tests;

// Well-known patterns in the format Board.Parse reads ('O' live, '.' dead), each in its smallest bounding box, and
// helpers that put a pattern on a larger, otherwise empty board.
internal static class Patterns
{
    // Still lifes: they never change.
    public const string Block = """
        OO
        OO
        """;

    public const string Beehive = """
        .OO.
        O..O
        .OO.
        """;

    public const string Loaf = """
        .OO.
        O..O
        .O.O
        ..O.
        """;

    public const string Boat = """
        OO.
        O.O
        .O.
        """;

    public const string Tub = """
        .O.
        O.O
        .O.
        """;

    // Oscillators, in one of their phases. The others need room around this bounding box.
    public const string Blinker = "OOO";

    public const string Toad = """
        .OOO
        OOO.
        """;

    public const string Beacon = """
        OO..
        OO..
        ..OO
        ..OO
        """;

    public const string Pulsar = """
        ..OOO...OOO..
        .............
        O....O.O....O
        O....O.O....O
        O....O.O....O
        ..OOO...OOO..
        .............
        ..OOO...OOO..
        O....O.O....O
        O....O.O....O
        O....O.O....O
        .............
        ..OOO...OOO..
        """;

    // A spaceship: every 4 generations it reappears one cell further down and to the right.
    public const string Glider = """
        .O.
        ..O
        OOO
        """;

    // Methuselahs: small patterns that take many generations to settle.
    public const string RPentomino = """
        .OO
        OO.
        .O.
        """;

    // Dies out completely after 130 generations on an unbounded grid.
    public const string Diehard = """
        ......O.
        OO......
        .O...OOO
        """;

    // A rows x columns board, dead except for the pattern, whose top-left cell is at (top, left).
    public static Board Place(string pattern, int rows, int columns, int top, int left)
    {
        Board shape = Board.Parse(pattern);
        return Board.Create(rows, columns, (row, column) =>
            row >= top && row < top + shape.Rows
            && column >= left && column < left + shape.Columns
            && shape[row - top, column - left]);
    }

    // The pattern with `margin` dead cells added on every side.
    public static Board Pad(string pattern, int margin)
    {
        Board shape = Board.Parse(pattern);
        return Place(pattern, shape.Rows + (2 * margin), shape.Columns + (2 * margin), margin, margin);
    }
}
