using System.Diagnostics;
using System.Text.Json.Serialization;

using GameOfLife.Api.Boards;
using GameOfLife.Api.Configuration;
using GameOfLife.Api.Persistence;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<GameOfLifeOptions>()
    .BindConfiguration(GameOfLifeOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Boards are stored in PostgreSQL. The connection string is read when the data source is first needed, by which time
// every configuration source, including the test hosts' overrides, has been loaded.
builder.Services.AddSingleton(services =>
{
    string connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("GameOfLife")
        ?? throw new InvalidOperationException(
            "The connection string ConnectionStrings:GameOfLife is missing. Set it in configuration, for example " +
            "with the environment variable ConnectionStrings__GameOfLife.");
    return new NpgsqlDataSourceBuilder(connectionString)
        .UseLoggerFactory(services.GetRequiredService<ILoggerFactory>())
        .Build();
});
builder.Services.AddSingleton<IBoardRepository, PostgresBoardRepository>();
builder.Services.AddScoped<BoardService>();

// At most MaxConcurrentSimulations simulations run at once; see SimulationLimit.
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<GameOfLifeOptions>>((limiter, settings) =>
        SimulationLimit.AddPolicy(limiter, settings.Value.MaxConcurrentSimulations));

// Every request is abandoned with 504 after RequestTimeoutSeconds. Abandoning cancels the request's token, which stops
// a simulation between generations and a database command that is waiting for a board's lock.
builder.Services.AddRequestTimeouts();
builder.Services.AddOptions<RequestTimeoutOptions>()
    .Configure<IOptions<GameOfLifeOptions>>((timeouts, settings) => timeouts.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(settings.Value.RequestTimeoutSeconds),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
    });

// Kestrel reads only its endpoints from the Kestrel section by itself; this binds the rest, including
// Limits.MaxRequestBodySize, which turns away larger uploads with 413 before they are read into memory.
builder.Services.Configure<KestrelServerOptions>(builder.Configuration.GetSection("Kestrel"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Reject numbers sent as strings, such as "1", instead of quietly converting them.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    // Write enums by name, such as "Oscillator", rather than by number.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Errors are RFC 9457 problem details. Each one carries the request's trace id, which also appears in the logs, so a
// client's error report can be matched to what the server logged. A request that cannot be read, such as malformed
// JSON, gets a 400 in every environment; by default Development throws an exception for it instead.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Game of Life API", Version = "v1" });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
});

WebApplication app = builder.Build();

// Creating the table when it is missing lets the app start against an empty database with no setup step.
await BoardsTable.EnsureCreatedAsync(app.Services.GetRequiredService<NpgsqlDataSource>(), CancellationToken.None);

// An unhandled exception becomes a 500 problem response that reveals no internal details. Error statuses written
// without a body, such as a 404 for an unknown route or a 400 for malformed JSON, get a problem body too.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRequestTimeouts();
app.UseRateLimiter();

// Off by default, on in Development.
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapBoardEndpoints();

app.Run();

// Public so the integration tests can host the app with WebApplicationFactory<Program>.
public partial class Program;
