using System.Net;
using System.Text.Json;

using GameOfLife.Api.Boards;
using GameOfLife.Api.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class FinalEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Final_SavesTheSettledStateAsTheBoardsCurrentGeneration()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.GliderOnTenByTen);

        BoardResponse final = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/final"));
        BoardResponse stored = await _client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));

        Assert.Equal(31, final.Generation);
        Assert.Equal(BoardStatus.StillLife, final.Status);
        Assert.Equal(1, final.Period);
        Assert.Equal(TestSupport.Cells(TestBoards.BlockInTheCorner), final.Cells);
        Assert.Equal((final.Generation, final.Status, final.Period), (stored.Generation, stored.Status, stored.Period));
        Assert.Equal(final.Cells, stored.Cells);
    }

    [Fact]
    public async Task Final_ForABoardAlreadyInItsCycle_KeepsItsCurrentGeneration()
    {
        // After one next, the blinker is at generation 1, which is already part of its two-generation cycle.
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);
        _ = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/next"));

        BoardResponse final = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/final"));

        Assert.Equal(1, final.Generation);
        Assert.Equal(BoardStatus.Oscillator, final.Status);
        Assert.Equal(2, final.Period);
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), final.Cells);
    }

    [Fact]
    public async Task Final_LoneCell_IsExtinctAtGenerationOne()
    {
        BoardResponse uploaded = await _client.UploadAsync("...\n.O.\n...");

        BoardResponse final = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/final"));

        Assert.Equal(1, final.Generation);
        Assert.Equal(BoardStatus.Extinct, final.Status);
        Assert.Equal(1, final.Period);
        Assert.Equal(0, final.LiveCells);
    }

    [Fact]
    public async Task Final_Repeated_ReturnsTheSavedStateAgain()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.GliderOnTenByTen);
        string final = TestSupport.BoardPath(uploaded.Id, "/final");

        BoardResponse first = await _client.PostForBoardAsync(final);
        BoardResponse second = await _client.PostForBoardAsync(final);

        Assert.Equal((first.Generation, first.Status, first.Period), (second.Generation, second.Status, second.Period));
        Assert.Equal(first.Cells, second.Cells);
    }

    [Fact]
    public async Task Final_BoardThatDoesNotSettleWithinTheMaximum_Returns422AndSavesNothing()
    {
        // The glider needs 32 generations to be seen settling, more than this host computes.
        using WebApplicationFactory<Program> limited = factory.WithSettings(("GameOfLife:MaxGenerations", "10"));
        using HttpClient client = limited.CreateClient();
        BoardResponse uploaded = await client.UploadAsync(TestBoards.GliderOnTenByTen);

        using HttpResponseMessage response = await client.PostAsync(TestSupport.BoardPath(uploaded.Id, "/final"), content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await TestSupport.ReadAsync<ProblemDetails>(response);
        Assert.Equal("Board did not settle", problem.Title);
        Assert.Equal(10, ((JsonElement)problem.Extensions["maxGenerations"]!).GetInt32());
        BoardResponse stored = await client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));
        Assert.Equal(0, stored.Generation);
        Assert.Equal(BoardStatus.Active, stored.Status);
    }
}
