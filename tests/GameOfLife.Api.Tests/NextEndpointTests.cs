using System.Net;
using System.Text.Json;

using GameOfLife.Api.Boards;
using GameOfLife.Api.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class NextEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Next_SavesTheNextGenerationAsTheBoardsCurrentOne()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);

        BoardResponse next = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/next"));
        BoardResponse stored = await _client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));

        Assert.Equal(1, next.Generation);
        Assert.Equal(BoardStatus.Active, next.Status);
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), next.Cells);
        Assert.Equal(next.Generation, stored.Generation);
        Assert.Equal(next.Cells, stored.Cells);
    }

    [Fact]
    public async Task Next_EachCallAdvancesTheBoardOneMoreGeneration()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);
        string next = TestSupport.BoardPath(uploaded.Id, "/next");

        BoardResponse first = await _client.PostForBoardAsync(next);
        BoardResponse second = await _client.PostForBoardAsync(next);
        BoardResponse third = await _client.PostForBoardAsync(next);

        long[] generations = [first.Generation, second.Generation, third.Generation];
        Assert.Equal([1L, 2L, 3L], generations);
        // The blinker turns between horizontal and vertical every generation.
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), first.Cells);
        Assert.Equal(TestSupport.Cells(TestBoards.VerticalBlinker), second.Cells);
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), third.Cells);
    }
}
