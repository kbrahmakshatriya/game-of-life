using System.Diagnostics;

using GameOfLife.Api.Configuration;
using GameOfLife.Api.Persistence;
using GameOfLife.Core;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Boards;

/// <summary>
/// The board endpoints. A board has a current generation: requests that change it are POSTs, and requests that only
/// read are GETs, which are safe to repeat.
/// </summary>
public static class BoardEndpoints
{
    private const string GetBoardRouteName = "GetBoard";

    /// <summary>Maps the board endpoints under <c>/api/v1/boards</c>.</summary>
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder boards = endpoints.MapGroup("/api/v1/boards").WithTags("Boards");

        boards.MapPost("/", UploadAsync)
            .WithName("UploadBoard")
            .WithSummary("Upload a board")
            .WithDescription("Stores the board as generation 0 of a new, active board and returns it with its id.")
            .ProducesValidationProblem();

        boards.MapGet("/{id}", GetBoardAsync)
            .WithName(GetBoardRouteName)
            .WithSummary("Get a board")
            .WithDescription("Returns the board's current generation and status.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        boards.MapPost("/{id}/next", NextAsync)
            .WithName("AdvanceBoard")
            .WithSummary("Advance the board one generation")
            .WithDescription(
                "Computes the next generation from the current one and saves it as the board's current generation.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        boards.MapGet("/{id}/ahead/{n}", PreviewAheadAsync)
            .WithName("PreviewAhead")
            .WithSummary("Preview the board n generations ahead")
            .WithDescription(
                "Computes the generation n after the board's current one, where n is from 0 to the configured " +
                "maximum. Nothing is saved.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        boards.MapPost("/{id}/ahead/{n}", JumpAheadAsync)
            .WithName("JumpAhead")
            .WithSummary("Advance the board n generations")
            .WithDescription(
                "Computes the generation n after the board's current one, where n is from 1 to the configured " +
                "maximum, and saves it as the board's current generation.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<CreatedAtRoute<BoardResponse>, ProblemHttpResult>> UploadAsync(
        UploadBoardRequest request, BoardService service, IOptions<GameOfLifeOptions> options,
        CancellationToken cancellationToken)
    {
        GameOfLifeOptions limits = options.Value;
        if (!CellMatrix.TryCreateBoard(request.Cells, limits.MaxRows, limits.MaxColumns, out Board? board, out FieldError? error))
        {
            return ValidationFailed(error);
        }

        StoredBoard stored = await service.CreateAsync(board, cancellationToken);
        return TypedResults.CreatedAtRoute(BoardResponse.From(stored), GetBoardRouteName, new { id = stored.Id });
    }

    private static async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> GetBoardAsync(
        Guid id, IBoardRepository repository, CancellationToken cancellationToken)
    {
        StoredBoard? stored = await repository.FindAsync(id, cancellationToken);
        return stored is null ? BoardNotFound(id) : TypedResults.Ok(BoardResponse.From(stored));
    }

    private static async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> NextAsync(
        Guid id, BoardService service, CancellationToken cancellationToken) =>
        ToHttpResult(id, await service.AdvanceAsync(id, 1, cancellationToken));

    private static async Task<Results<Ok<PreviewResponse>, ProblemHttpResult>> PreviewAheadAsync(
        Guid id, int n, IBoardRepository repository, IOptions<GameOfLifeOptions> options,
        CancellationToken cancellationToken)
    {
        int maxGenerations = options.Value.MaxGenerations;
        if (n < 0 || n > maxGenerations)
        {
            return ValidationFailed(new FieldError("n", $"n must be from 0 to {maxGenerations}; it is {n}."));
        }

        StoredBoard? current = await repository.FindAsync(id, cancellationToken);
        if (current is null)
        {
            return BoardNotFound(id);
        }

        Board ahead = Simulation.Advance(current.Board, n, cancellationToken);
        return TypedResults.Ok(PreviewResponse.From(id, current.Generation + n, ahead));
    }

    private static async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> JumpAheadAsync(
        Guid id, int n, BoardService service, IOptions<GameOfLifeOptions> options,
        CancellationToken cancellationToken)
    {
        int maxGenerations = options.Value.MaxGenerations;
        if (n < 1 || n > maxGenerations)
        {
            return ValidationFailed(new FieldError("n", $"n must be from 1 to {maxGenerations}; it is {n}."));
        }

        return ToHttpResult(id, await service.AdvanceAsync(id, n, cancellationToken));
    }

    private static Results<Ok<BoardResponse>, ProblemHttpResult> ToHttpResult(
        Guid id, BoardUpdateResult result) => result switch
        {
            BoardUpdateResult.Succeeded succeeded => TypedResults.Ok(BoardResponse.From(succeeded.Board)),
            BoardUpdateResult.NotFound => BoardNotFound(id),
            _ => throw new UnreachableException($"No response is defined for {result}."),
        };

    // Written as a problem result rather than TypedResults.ValidationProblem, which in .NET 8 bypasses the problem
    // details service and so would leave out the trace id that every other error carries.
    private static ProblemHttpResult ValidationFailed(FieldError error) => TypedResults.Problem(
        new HttpValidationProblemDetails(new Dictionary<string, string[]> { [error.Field] = [error.Message] })
        {
            Status = StatusCodes.Status400BadRequest,
        });

    private static ProblemHttpResult BoardNotFound(Guid id) => TypedResults.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Board not found",
        detail: $"No board has the id {id}.");
}
