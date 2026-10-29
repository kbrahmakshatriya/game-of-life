using System.Net;
using System.Text.Json;

using GameOfLife.Api.Boards;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
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
    [InlineData("GET", "/next")] // next and final change the board, so they only accept POST
    [InlineData("GET", "/final")]
    [InlineData("DELETE", "")]
    public async Task UnsupportedMethod_Returns405ProblemDetails(string method, string endpoint)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), TestSupport.BoardPath(Guid.NewGuid(), endpoint));

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Responses_UseCamelCaseNamesWriteEnumsByNameAndLeaveOutAnAbsentPeriod()
    {
        BoardResponse uploaded = await _client.UploadAsync(TestBoards.Block);
        using JsonDocument active = JsonDocument.Parse(await _client.GetStringAsync(TestSupport.BoardPath(uploaded.Id)));
        using HttpResponseMessage response = await _client.PostAsync(TestSupport.BoardPath(uploaded.Id, "/final"), content: null);
        using JsonDocument settled = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(["id", "generation", "status", "rows", "columns", "liveCells", "cells"], PropertyNames(active));
        Assert.Equal(["id", "generation", "status", "period", "rows", "columns", "liveCells", "cells"], PropertyNames(settled));
        Assert.Equal("Active", active.RootElement.GetProperty("status").GetString());
        Assert.Equal("StillLife", settled.RootElement.GetProperty("status").GetString());
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
                ["/api/v1/boards/{id}/next"] = ["post"],
                ["/api/v1/boards/{id}/ahead/{n}"] = ["get", "post"],
                ["/api/v1/boards/{id}/final"] = ["post"],
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
    [InlineData("GameOfLife:MaxGenerations", "0")]
    [InlineData("GameOfLife:MaxConcurrentSimulations", "0")]
    [InlineData("GameOfLife:MaxConcurrentSimulations", "101")]
    [InlineData("GameOfLife:RequestTimeoutSeconds", "0")]
    public void InvalidLimitSetting_StopsTheAppFromStarting(string key, string value)
    {
        using WebApplicationFactory<Program> misconfigured = factory.WithSettings((key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => misconfigured.CreateClient());
        Assert.Contains(key.Split(':')[1], exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UploadSizeLimit_IsReadFromConfiguration()
    {
        // The in-memory test server does not enforce Kestrel's limits, so check that the setting reaches Kestrel.
        _ = factory.CreateClient();

        var kestrel = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        Assert.Equal(1_048_576, kestrel.Limits.MaxRequestBodySize);
    }

    private static string[] PropertyNames(JsonDocument json) =>
        json.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
}
