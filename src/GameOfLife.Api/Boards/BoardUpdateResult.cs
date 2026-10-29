using GameOfLife.Api.Persistence;

namespace GameOfLife.Api.Boards;

/// <summary>
/// What happened to a request that changes a board. The cases are a closed set: the private constructor means
/// nothing outside this type can add another, so the endpoints can map each case to a response.
/// </summary>
public abstract record BoardUpdateResult
{
    private BoardUpdateResult()
    {
    }

    /// <summary>The request succeeded, and <paramref name="Board"/> is the board as it is now stored.</summary>
    public sealed record Succeeded(StoredBoard Board) : BoardUpdateResult;

    /// <summary>No board has the id.</summary>
    public sealed record NotFound : BoardUpdateResult;

    /// <summary>The board has settled, so it cannot advance. <paramref name="Board"/> is its final state.</summary>
    public sealed record AlreadySettled(StoredBoard Board) : BoardUpdateResult;

    /// <summary>No generation repeated within <paramref name="MaxGenerations"/>, so nothing was saved.</summary>
    public sealed record DidNotSettle(int MaxGenerations) : BoardUpdateResult;
}
