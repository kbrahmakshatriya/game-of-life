using System.Text.Json;

namespace GameOfLife.Api.Errors;

/// <summary>
/// Turns a request that the framework could not read into a problem response that says what is wrong: a body that is
/// not valid JSON or has a value of the wrong type, an id or <c>n</c> that cannot be read, or a body that is not JSON.
/// </summary>
/// <remarks>
/// With <see cref="RouteHandlerOptions.ThrowOnBadRequest"/> on, the framework reports such a request by throwing a
/// <see cref="BadHttpRequestException"/>. Catching it here, just before the endpoints, answers the client before the
/// exception handler would log the client's mistake as a server error.
/// </remarks>
public static class UnreadableRequests
{
    /// <summary>Adds the middleware. It must run before the endpoints.</summary>
    public static IApplicationBuilder UseUnreadableRequestProblems(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = exception.StatusCode;
                await context.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = { Status = exception.StatusCode, Detail = Describe(exception) },
                    });
            }
        });
    }

    private static string Describe(BadHttpRequestException exception)
    {
        if (exception.InnerException is not JsonException json)
        {
            // The framework's own message already names the problem, for example
            // 'Failed to bind parameter "Guid id" from "not-a-guid".'
            return exception.Message;
        }

        // JSON paths look like $.cells[0][1]; the field is the part after "$.", in the same form the grid errors use.
        string where = json.Path is { } path && path.StartsWith("$.", StringComparison.Ordinal)
            ? $"The request body at {path[2..]}"
            : "The request body";
        return $"{where} could not be read. Send JSON such as {{\"cells\": [[0, 1], [1, 0]]}}, " +
            "where every cell is the number 0 or 1.";
    }
}
