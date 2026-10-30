using Microsoft.AspNetCore.Diagnostics;

using Npgsql;

namespace GameOfLife.Api.Errors;

/// <summary>
/// Answers a request that failed because the database could not be reached with 503 and <c>Retry-After</c>, instead of
/// the 500 an unexpected failure gets: the condition is temporary, and the request can be repeated once the database is
/// back.
/// </summary>
/// <remarks>
/// Npgsql marks failures such as a refused connection, a timeout or a server that is shutting down as transient. Every
/// other exception is left to the default handling, which answers 500.
/// </remarks>
public sealed class DatabaseUnavailableHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    // Longer than the simulation limit's one second: an outage usually lasts longer than a busy moment.
    private const string RetryAfterSeconds = "5";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (exception is not NpgsqlException { IsTransient: true })
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds;
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Database unavailable",
                // The exception's own message can name the database's host, so it only goes to the log.
                Detail = "The database cannot be reached right now. Try again shortly.",
            },
        });
        return true;
    }
}
