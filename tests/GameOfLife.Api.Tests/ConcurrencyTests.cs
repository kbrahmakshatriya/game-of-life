using System.Net;

using GameOfLife.Api.Boards;
using GameOfLife.Core;

using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

namespace GameOfLife.Api.Tests;

// Changes to one board take its row lock, so they happen one at a time and none is lost.
[Collection(PostgresFixture.CollectionName)]
public sealed class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ConcurrentNexts_ForOneBoard_EachSaveTheirOwnGeneration()
    {
        const int Requests = 25;
        HttpClient client = _client;
        BoardResponse uploaded = await client.UploadAsync(TestBoards.GliderOnTenByTen);
        string next = TestSupport.BoardPath(uploaded.Id, "/next");

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, Requests).Select(_ => client.PostAsync(next, content: null)));
        try
        {
            // Every request waited its turn and then saved the generation after the one before it.
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            BoardResponse[] advanced = await Task.WhenAll(responses.Select(TestSupport.ReadAsync<BoardResponse>));
            Assert.Equal(Enumerable.Range(1, Requests).Select(generation => (long)generation),
                advanced.Select(board => board.Generation).Order());

            BoardResponse stored = await client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));
            Assert.Equal(Requests, stored.Generation);
            Board expected = Simulation.Advance(Board.Parse(TestBoards.GliderOnTenByTen), Requests);
            Assert.Equal(CellMatrix.FromBoard(expected), stored.Cells);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Next_WhileAnotherTransactionHoldsTheBoard_WaitsAndThenStartsFromWhatItSaved()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SELECT 1 FROM boards WHERE id = $1 FOR UPDATE", uploaded.Id);

        // Another "request" holds the board's lock, so next has to wait for it.
        Task<HttpResponseMessage> next = _client.PostAsync(TestSupport.BoardPath(uploaded.Id, "/next"), content: null);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(next.IsCompleted, "next finished while another transaction held the board's lock.");

        // The other request saves generation 5, a horizontal blinker, and releases the lock.
        await ExecuteAsync(connection, transaction, "UPDATE boards SET generation = 5, cells = $2 WHERE id = $1",
            uploaded.Id, Board.Parse(TestBoards.HorizontalBlinker).ToString());
        await transaction.CommitAsync();

        using HttpResponseMessage response = await next;
        BoardResponse advanced = await TestSupport.ReadAsync<BoardResponse>(response);
        // next started from the saved generation 5, not from the generation 0 it would have read without the lock.
        Assert.Equal(6, advanced.Generation);
        Assert.Equal(TestSupport.Cells(TestBoards.VerticalBlinker), advanced.Cells);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (object parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }
}
