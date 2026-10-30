using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using GameOfLife.Api.Boards;
using GameOfLife.Core;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

internal static class TestSupport
{
    public const string BoardsPath = "/api/v1/boards";

    // How the API writes JSON: camelCase names and enums by name.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // The API hosted in memory with some settings replaced, such as ("GameOfLife:MaxGenerations", "10").
    public static WebApplicationFactory<Program> WithSettings(
        this WebApplicationFactory<Program> factory, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    // The path of a board, or of one of its endpoints, such as "/next".
    public static string BoardPath(Guid id, string endpoint = "") => $"{BoardsPath}/{id}{endpoint}";

    // Uploads a board written in Board.Parse's format ('O' live, '.' dead) and returns the API's response.
    public static async Task<BoardResponse> UploadAsync(this HttpClient client, string pattern)
    {
        var request = new UploadBoardRequest(Cells(pattern));
        using HttpResponseMessage response = await client.PostAsJsonAsync(BoardsPath, request, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<BoardResponse>(response);
    }

    // POSTs to an endpoint that takes no body, such as next or final, and returns the board it answers with.
    public static async Task<BoardResponse> PostForBoardAsync(this HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.PostAsync(path, content: null);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<BoardResponse>(response);
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string path) =>
        await client.GetFromJsonAsync<T>(path, Json) ?? throw new InvalidOperationException("The response has no body.");

    public static async Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string path, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(path, content);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json)
        ?? throw new InvalidOperationException("The response has no body.");

    // The cells of a board written in Board.Parse's format, as the API writes them.
    public static int[][] Cells(string pattern) => Board.Parse(pattern).ToMatrix();
}
