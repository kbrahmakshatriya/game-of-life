using Microsoft.AspNetCore.RateLimiting;

namespace GameOfLife.Api.Boards;

/// <summary>
/// Caps how many simulations run at once. Next, ahead and final compute generations on the request's thread and hold
/// a database connection while they do, so the cap bounds both CPU use and connections. Requests over the cap are
/// turned away at once with 503 instead of queuing, so a burst cannot pile up behind slow simulations.
/// </summary>
public static partial class SimulationLimit
{
    public const string PolicyName = "simulations";

    /// <summary>Adds the policy that <see cref="BoardEndpoints"/> puts on every endpoint that simulates.</summary>
    public static void AddPolicy(RateLimiterOptions options, int maxConcurrentSimulations)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
        options.OnRejected = RejectAsync;
        options.AddConcurrencyLimiter(PolicyName, limiter =>
        {
            limiter.PermitLimit = maxConcurrentSimulations;
            limiter.QueueLimit = 0;
        });
    }

    // The limiter has already set the status to 503. Retry-After tells the client the condition is temporary.
    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext http = context.HttpContext;
        http.Response.Headers.RetryAfter = "1";

        ILogger logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SimulationLimit));
        LogRejected(logger, http.Request.Method, http.Request.Path.Value ?? "");

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Too many simulations",
                Detail = "The service is already running as many simulations as it allows. Try again shortly.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turned away {Method} {Path}: the simulation limit was reached")]
    private static partial void LogRejected(ILogger logger, string method, string path);
}
