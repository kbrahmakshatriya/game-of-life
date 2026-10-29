using GameOfLife.Core;

namespace GameOfLife.Api.Persistence;

/// <summary>A board as stored: its current generation, and whether it has settled.</summary>
/// <param name="Id">The board's id.</param>
/// <param name="Generation">The number of the current generation; the uploaded board is generation 0.</param>
/// <param name="Status">Active until the final state is saved, then how the board settled.</param>
/// <param name="Period">For a settled board, how many generations its cycle takes; null while it is active.</param>
/// <param name="Board">The cells of the current generation.</param>
public sealed record StoredBoard(Guid Id, long Generation, BoardStatus Status, int? Period, Board Board);
