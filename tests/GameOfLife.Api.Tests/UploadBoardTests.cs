using System.Net;
using System.Net.Http.Json;

using GameOfLife.Api.Boards;
using GameOfLife.Api.Persistence;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class UploadBoardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upload_ValidBoard_Returns201WithItsLocationAndGenerationZero()
    {
        int[][] cells = [[0, 1, 0], [0, 1, 0], [0, 1, 0]];

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            TestSupport.BoardsPath, new UploadBoardRequest(cells), TestSupport.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        BoardResponse board = await TestSupport.ReadAsync<BoardResponse>(response);
        Assert.Equal($"{TestSupport.BoardsPath}/{board.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(0, board.Generation);
        Assert.Equal(BoardStatus.Active, board.Status);
        Assert.Null(board.Period);
        Assert.Equal(3, board.Rows);
        Assert.Equal(3, board.Columns);
        Assert.Equal(3, board.LiveCells);
        Assert.Equal(cells, board.Cells);
    }

    [Fact]
    public async Task Upload_ThenGetItsLocation_ReturnsTheSameBoard()
    {
        using HttpResponseMessage created = await _client.PostAsJsonAsync(
            TestSupport.BoardsPath, new UploadBoardRequest(TestSupport.Cells("O.O\n.O.")), TestSupport.Json);
        BoardResponse uploaded = await TestSupport.ReadAsync<BoardResponse>(created);

        BoardResponse fetched = await _client.GetFromJsonAsync<BoardResponse>(created.Headers.Location, TestSupport.Json)
            ?? throw new InvalidOperationException("The response has no body.");

        Assert.Equal(uploaded.Id, fetched.Id);
        Assert.Equal(0, fetched.Generation);
        Assert.Equal(uploaded.Cells, fetched.Cells);
    }

    [Fact]
    public async Task Upload_SameBoardTwice_StoresTwoBoardsWithDifferentIds()
    {
        BoardResponse first = await _client.UploadAsync("OO\nOO");
        BoardResponse second = await _client.UploadAsync("OO\nOO");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("""{}""", "cells")]
    [InlineData("null", "cells")]
    [InlineData("", "cells")]
    [InlineData("""{"cells":[]}""", "cells")]
    [InlineData("""{"cells":[[0,1],null]}""", "cells[1]")]
    [InlineData("""{"cells":[[0,1],[1]]}""", "cells[1]")]
    [InlineData("""{"cells":[[0,2]]}""", "cells[0][1]")]
    public async Task Upload_InvalidCells_Returns400NamingTheField(string json, string field)
    {
        using HttpResponseMessage response = await _client.PostJsonAsync(TestSupport.BoardsPath, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await TestSupport.ReadAsync<HttpValidationProblemDetails>(response);
        Assert.Equal(field, Assert.Single(problem.Errors).Key);
    }

    [Theory]
    [InlineData(4, 4, "cells")] // one row too many
    [InlineData(3, 5, "cells[0]")] // one column too many
    public async Task Upload_BoardLargerThanTheConfiguredLimits_Returns400(int rows, int columns, string field)
    {
        using WebApplicationFactory<Program> limited = factory.WithSettings(
            ("GameOfLife:MaxRows", "3"), ("GameOfLife:MaxColumns", "4"));
        using HttpClient client = limited.CreateClient();
        int[][] cells = Enumerable.Range(0, rows).Select(_ => new int[columns]).ToArray();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            TestSupport.BoardsPath, new UploadBoardRequest(cells), TestSupport.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await TestSupport.ReadAsync<HttpValidationProblemDetails>(response);
        Assert.Equal(field, Assert.Single(problem.Errors).Key);
    }

    [Theory]
    [InlineData("""{"cells":[["1","0"]]}""", "The request body at cells[0][0] could not be read")] // numbers as strings
    [InlineData("""{"cells":[[1,true]]}""", "The request body at cells[0][1] could not be read")]
    [InlineData("""{"cells":"OO"}""", "The request body at cells could not be read")]
    [InlineData("""{"cells":[[0,1]""", "could not be read")] // malformed JSON
    [InlineData("[[0,1],[1,0]]", "The request body could not be read")] // the grid without the object around it
    public async Task Upload_BodyThatCannotBeRead_Returns400SayingWhere(string json, string detail)
    {
        using HttpResponseMessage response = await _client.PostJsonAsync(TestSupport.BoardsPath, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await TestSupport.ReadAsync<ProblemDetails>(response);
        Assert.Contains(detail, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_WithoutAJsonContentType_Returns415()
    {
        using var content = new StringContent("""{"cells":[[1]]}""");

        using HttpResponseMessage response = await _client.PostAsync(TestSupport.BoardsPath, content);

        // Routing turns the request away before the endpoint runs, because the endpoint only accepts JSON.
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
