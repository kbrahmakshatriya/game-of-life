using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

// Every endpoint that takes a board id answers the same way for an id that is unknown or cannot be read.
[Collection(PostgresFixture.CollectionName)]
public sealed class BoardIdTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("GET", "")]
    public async Task UnknownId_Returns404ProblemDetails(string method, string endpoint)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), TestSupport.BoardPath(Guid.NewGuid(), endpoint));

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("GET", "/not-a-guid")]
    public async Task IdOrNThatCannotBeRead_Returns400ProblemDetails(string method, string endpoint)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{TestSupport.BoardsPath}{endpoint}");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
