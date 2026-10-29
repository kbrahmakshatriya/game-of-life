using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

// Hosts the API in memory on a PostgreSQL database of its own.
public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The connection string of this host's database, for tests that look at the database directly.
    public string ConnectionString { get; private set; } = "";

    // xUnit runs this before the first test that uses the factory, so before the host is built.
    public async ValueTask InitializeAsync() => ConnectionString = await postgres.CreateDatabaseAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:GameOfLife", ConnectionString);
}
