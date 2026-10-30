using GameOfLife.Api.Boards;
using GameOfLife.Api.Persistence;
using GameOfLife.Core;

using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Boards_SurviveARestartOfTheApp()
    {
        Guid active;
        Guid settled;

        // The first app instance uploads two boards, jumps one ahead and settles the other, then shuts down.
        await using (WebApplicationFactory<Program> first = factory.WithWebHostBuilder(_ => { }))
        {
            using HttpClient client = first.CreateClient();
            active = (await client.UploadAsync(TestBoards.GliderOnTenByTen)).Id;
            _ = await client.PostForBoardAsync(TestSupport.BoardPath(active, "/ahead/5"));
            settled = (await client.UploadAsync(TestBoards.VerticalBlinker)).Id;
            _ = await client.PostForBoardAsync(TestSupport.BoardPath(settled, "/final"));
        }

        // A second instance, which shares no memory with the first, reads both back from the database.
        await using WebApplicationFactory<Program> second = factory.WithWebHostBuilder(_ => { });
        using HttpClient restarted = second.CreateClient();
        BoardResponse activeBoard = await restarted.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(active));
        BoardResponse settledBoard = await restarted.GetJsonAsync<BoardResponse>(TestSupport.BoardPath(settled));

        Assert.Equal(5, activeBoard.Generation);
        Assert.Equal(BoardStatus.Active, activeBoard.Status);
        Board expected = Simulation.Advance(Board.Parse(TestBoards.GliderOnTenByTen), 5);
        Assert.Equal(expected.ToMatrix(), activeBoard.Cells);
        Assert.Equal(0, settledBoard.Generation);
        Assert.Equal(BoardStatus.Oscillator, settledBoard.Status);
        Assert.Equal(2, settledBoard.Period);
        Assert.Equal(TestSupport.Cells(TestBoards.VerticalBlinker), settledBoard.Cells);
    }
}
