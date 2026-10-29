using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Tests;

// Behavior that belongs to the host as a whole rather than to one endpoint.
[Collection(PostgresFixture.CollectionName)]
public sealed class HostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("DELETE", "")]
    public async Task UnsupportedMethod_Returns405ProblemDetails(string method, string endpoint)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), TestSupport.BoardPath(Guid.NewGuid(), endpoint));

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesEveryBoardEndpointWithItsMethod()
    {
        using JsonDocument document = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        JsonElement root = document.RootElement;

        Dictionary<string, string[]> operations = root.GetProperty("paths").EnumerateObject().ToDictionary(
            path => path.Name, path => path.Value.EnumerateObject().Select(operation => operation.Name).Order().ToArray());
        Assert.Equal(
            new Dictionary<string, string[]>
            {
                ["/api/v1/boards"] = ["post"],
                ["/api/v1/boards/{id}"] = ["get"],
            },
            operations);
        // The document must describe enums the way the API writes them: by name.
        JsonElement status = root.GetProperty("components").GetProperty("schemas").GetProperty("BoardStatus");
        Assert.Equal(
            ["Active", "Extinct", "StillLife", "Oscillator"],
            status.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    [Theory]
    [InlineData("GameOfLife:MaxRows", "0")]
    [InlineData("GameOfLife:MaxColumns", "-1")]
    public void InvalidLimitSetting_StopsTheAppFromStarting(string key, string value)
    {
        using WebApplicationFactory<Program> misconfigured = factory.WithSettings((key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => misconfigured.CreateClient());
        Assert.Contains(key.Split(':')[1], exception.Message, StringComparison.Ordinal);
    }
}
