using System.Diagnostics;
using System.Net;
using System.Text.Json;

using GameOfLife.Api.Boards;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

namespace GameOfLife.Api.Tests;

// The limits that protect the service: the cap on simulations running at once, the request timeout, and the trace
// id that ties an error response to the server's logs. Holding a board's row lock from the test puts a request into
// a known state, so none of these depend on timing luck.
[Collection(PostgresFixture.CollectionName)]
public sealed class HardeningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task SimulationOverTheLimit_IsTurnedAwayWith503AndRetryAfter()
    {
        using WebApplicationFactory<Program> limited = factory.WithSettings(("GameOfLife:MaxConcurrentSimulations", "1"));
        using HttpClient client = limited.CreateClient();
        BoardResponse held = await client.UploadAsync(TestBoards.VerticalBlinker);
        BoardResponse other = await client.UploadAsync(TestBoards.Block);
        await using BoardRowLock rowLock = await BoardRowLock.TakeAsync(factory.ConnectionString, held.Id);

        // This request takes the only simulation slot, then waits for the row lock the test holds.
        Task<HttpResponseMessage> first = client.PostAsync(TestSupport.BoardPath(held.Id, "/next"), content: null);
        await TestSupport.WaitUntilARequestWaitsForALockAsync(factory.ConnectionString);

        using HttpResponseMessage second = await client.GetAsync(TestSupport.BoardPath(other.Id, "/ahead/1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.Equal("1", second.Headers.RetryAfter?.ToString());
        var problem = await TestSupport.ReadAsync<ProblemDetails>(second);
        Assert.Equal("Too many simulations", problem.Title);

        // Once the lock is released, the first request finishes normally.
        await rowLock.ReleaseAsync();
        using HttpResponseMessage firstResponse = await first;
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    [Fact]
    public async Task RequestThatRunsTooLong_IsAbandonedWith504AndSavesNothing()
    {
        using WebApplicationFactory<Program> impatient = factory.WithSettings(("GameOfLife:RequestTimeoutSeconds", "1"));
        using HttpClient client = impatient.CreateClient();
        // Without the server's timeout the request would wait for the test's lock forever; fail in 15 s instead.
        client.Timeout = TimeSpan.FromSeconds(15);
        BoardResponse uploaded = await client.UploadAsync(TestBoards.VerticalBlinker);
        await using BoardRowLock rowLock = await BoardRowLock.TakeAsync(factory.ConnectionString, uploaded.Id);

        // next waits for the lock the test holds until the one-second timeout abandons it.
        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.PostAsync(TestSupport.BoardPath(uploaded.Id, "/next"), content: null);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9));

        await rowLock.ReleaseAsync();
        BoardResponse stored = await client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));
        Assert.Equal(0, stored.Generation);
        // The abandoned request gave its connection back, so the board can be advanced normally.
        BoardResponse next = await client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/next"));
        Assert.Equal(1, next.Generation);
    }

    [Theory]
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000")] // written by an endpoint: board not found
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000/ahead/-1")] // a validation error: n is out of range
    [InlineData("/api/v1/boards/not-a-guid")] // written for an empty 400: the id cannot be read
    [InlineData("/api/v1/nothing-here")] // written for an empty 404: no such route
    public async Task ErrorResponses_CarryTheRequestsTraceId(string path)
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? traceId = problem.RootElement.GetProperty("traceId").GetString();
        // A W3C trace context id: version, 32-digit trace id, 16-digit span id, flags.
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", traceId);
    }

    // A transaction in the test that holds a board's row lock, the way a change in progress would.
    private sealed class BoardRowLock : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;

        private BoardRowLock(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public static async Task<BoardRowLock> TakeAsync(string connectionString, Guid boardId)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("SELECT 1 FROM boards WHERE id = $1 FOR UPDATE", connection, transaction);
            command.Parameters.AddWithValue(boardId);
            await command.ExecuteNonQueryAsync();
            return new BoardRowLock(connection, transaction);
        }

        public Task ReleaseAsync() => _transaction.RollbackAsync();

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
