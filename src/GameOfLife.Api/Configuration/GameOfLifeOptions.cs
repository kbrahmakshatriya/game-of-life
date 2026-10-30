using System.ComponentModel.DataAnnotations;

namespace GameOfLife.Api.Configuration;

/// <summary>
/// Limits on the work requests can ask for, read from the <c>GameOfLife</c> configuration section and checked when the
/// app starts, so a bad setting stops startup instead of failing requests later.
/// </summary>
public sealed class GameOfLifeOptions
{
    public const string SectionName = "GameOfLife";

    // The upper bounds of the ranges only catch settings that are plainly wrong; 10,000 x 10,000 cells is still far
    // more than a request should simulate. appsettings.json holds the values actually used.
    [Range(1, 10_000)]
    public int MaxRows { get; set; }

    [Range(1, 10_000)]
    public int MaxColumns { get; set; }

    // The most generations one request computes: the largest n for "n generations ahead", and how far the final
    // state search looks before giving up.
    [Range(1, 1_000_000)]
    public int MaxGenerations { get; set; }

    // How many simulations (next, ahead and final requests) may run at once; more are turned away with 503. Each one
    // uses a CPU core while it runs, and every one except a preview also holds a database connection, so this can be
    // at most the connection pool's size, which is 100 unless the connection string says otherwise. Values near that
    // leave few connections for uploads, reads and health checks, which share the pool.
    [Range(1, 100)]
    public int MaxConcurrentSimulations { get; set; }

    // How long a request may take before it is abandoned with 504, including any time spent waiting for a board that
    // another request is changing.
    [Range(1, 3_600)]
    public int RequestTimeoutSeconds { get; set; }
}
