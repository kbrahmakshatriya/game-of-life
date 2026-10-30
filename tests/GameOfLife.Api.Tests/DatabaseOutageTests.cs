using System.Net;
using System.Net.Sockets;

using GameOfLife.Api.Persistence;
using GameOfLife.Core;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace GameOfLife.Api.Tests;

// Requests made while the database cannot be reached. The repository is replaced by one that fails the way Npgsql does
// when its connection is refused, so the shared PostgreSQL container keeps running for the other tests.
[Collection(PostgresFixture.CollectionName)]
public sealed class DatabaseOutageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/next")]
    [InlineData("GET", "/ahead/1")]
    [InlineData("POST", "/ahead/1")]
    [InlineData("POST", "/final")]
    public async Task BoardRequest_WhileTheDatabaseIsUnreachable_Returns503WithRetryAfter(string method, string endpoint)
    {
        using WebApplicationFactory<Program> outage = WithRepository(new FailingRepository(Unreachable()));
        using HttpClient client = outage.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), TestSupport.BoardPath(Guid.NewGuid(), endpoint));

        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertDatabaseUnavailableAsync(response);
    }

    [Fact]
    public async Task Upload_WhileTheDatabaseIsUnreachable_Returns503WithRetryAfter()
    {
        using WebApplicationFactory<Program> outage = WithRepository(new FailingRepository(Unreachable()));
        using HttpClient client = outage.CreateClient();

        using HttpResponseMessage response = await client.PostJsonAsync(TestSupport.BoardsPath, """{"cells":[[1]]}""");

        await AssertDatabaseUnavailableAsync(response);
    }

    [Fact]
    public async Task OtherFailures_StillReturn500WithoutTheirDetails()
    {
        using WebApplicationFactory<Program> broken = WithRepository(
            new FailingRepository(new InvalidOperationException("an internal detail")));
        using HttpClient client = broken.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(TestSupport.BoardPath(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("an internal detail", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task AssertDatabaseUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.RetryAfter?.ToString());
        var problem = await TestSupport.ReadAsync<ProblemDetails>(response);
        Assert.Equal("Database unavailable", problem.Title);
    }

    // What Npgsql throws when nothing accepts its connection: an NpgsqlException that it marks as transient.
    private static NpgsqlException Unreachable() =>
        new("Failed to connect to the database.", new SocketException((int)SocketError.ConnectionRefused));

    private WebApplicationFactory<Program> WithRepository(IBoardRepository repository) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(repository)));

    // Fails every call with the same exception.
    private sealed class FailingRepository(Exception failure) : IBoardRepository
    {
        public Task<StoredBoard> AddAsync(Board board, CancellationToken cancellationToken) =>
            Task.FromException<StoredBoard>(failure);

        public Task<StoredBoard?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException<StoredBoard?>(failure);

        public Task<IBoardLock?> LockAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException<IBoardLock?>(failure);
    }
}
