using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace GameOfLife.Api.Health;

/// <summary>Reports whether the database answers a trivial query, which every board request depends on.</summary>
public sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("The database did not answer.", exception);
        }
    }
}
