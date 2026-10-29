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
}
