using System.Net;

using GameOfLife.Api.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace GameOfLife.Api.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_WithTheDatabaseUp_AreHealthy(string path)
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DatabaseCheck_WhenTheDatabaseDoesNotAnswer_IsUnhealthy()
    {
        // Nothing listens on port 1, so the connection is refused at once.
        await using NpgsqlDataSource unreachable = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");

        HealthCheckResult result = await new DatabaseHealthCheck(unreachable).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.IsAssignableFrom<NpgsqlException>(result.Exception);
    }
}
