using System.Net;

using GameOfLife.Api.Boards;
using GameOfLife.Core;

using Microsoft.AspNetCore.Http;

namespace GameOfLife.Api.Tests;

// GET ahead/{n} previews a generation without saving it; POST ahead/{n} jumps the board there and saves it.
[Collection(PostgresFixture.CollectionName)]
public sealed class AheadEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(0, TestBoards.VerticalBlinker)]
    [InlineData(1, TestBoards.HorizontalBlinker)]
    [InlineData(2, TestBoards.VerticalBlinker)]
    [InlineData(1_000, TestBoards.VerticalBlinker)] // the configured maximum
    public async Task Preview_ReturnsTheGenerationNAfterTheCurrentOne(int n, string expected)
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);

        PreviewResponse preview = await _client.GetJsonAsync<PreviewResponse>(TestSupport.BoardPath(uploaded.Id, $"/ahead/{n}"));

        Assert.Equal(n, preview.Generation);
        Assert.Equal(TestSupport.Cells(expected), preview.Cells);
    }

    [Fact]
    public async Task Preview_CountsFromTheCurrentGenerationAndSavesNothing()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);
        _ = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/next"));

        PreviewResponse preview = await _client.GetJsonAsync<PreviewResponse>(TestSupport.BoardPath(uploaded.Id, "/ahead/2"));
        BoardResponse stored = await _client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));

        // The board is at generation 1, so two ahead is generation 3, and the board stays at 1.
        Assert.Equal(3, preview.Generation);
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), preview.Cells);
        Assert.Equal(1, stored.Generation);
        Assert.Equal(TestSupport.Cells(TestBoards.HorizontalBlinker), stored.Cells);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_001)]
    public async Task Preview_NOutsideZeroToTheMaximum_Returns400(int n)
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);

        using HttpResponseMessage response = await _client.GetAsync(TestSupport.BoardPath(uploaded.Id, $"/ahead/{n}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await TestSupport.ReadAsync<HttpValidationProblemDetails>(response);
        Assert.Equal("n", Assert.Single(problem.Errors).Key);
    }

    [Fact]
    public async Task Jump_SavesTheGenerationNAfterTheCurrentOne()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.GliderOnTenByTen);

        BoardResponse jumped = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/ahead/5"));
        BoardResponse stored = await _client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));

        Board expected = Simulation.Advance(Board.Parse(TestBoards.GliderOnTenByTen), 5);
        Assert.Equal(5, jumped.Generation);
        Assert.Equal(CellMatrix.FromBoard(expected), jumped.Cells);
        Assert.Equal(jumped.Generation, stored.Generation);
        Assert.Equal(jumped.Cells, stored.Cells);
    }

    [Fact]
    public async Task Jump_LandsWhereThePreviewSaidItWould()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.GliderOnTenByTen);
        _ = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/next"));

        PreviewResponse preview = await _client.GetJsonAsync<PreviewResponse>(TestSupport.BoardPath(uploaded.Id, "/ahead/7"));
        BoardResponse jumped = await _client.PostForBoardAsync(TestSupport.BoardPath(uploaded.Id, "/ahead/7"));

        Assert.Equal(8, jumped.Generation);
        Assert.Equal(preview.Generation, jumped.Generation);
        Assert.Equal(preview.Cells, jumped.Cells);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public async Task Jump_NOutsideOneToTheMaximum_Returns400AndSavesNothing(int n)
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.VerticalBlinker);

        using HttpResponseMessage response = await _client.PostAsync(TestSupport.BoardPath(uploaded.Id, $"/ahead/{n}"), content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await TestSupport.ReadAsync<HttpValidationProblemDetails>(response);
        Assert.Equal("n", Assert.Single(problem.Errors).Key);
        BoardResponse stored = await _client.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(uploaded.Id));
        Assert.Equal(0, stored.Generation);
    }
}
