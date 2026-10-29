using GameOfLife.Api.Persistence;
using GameOfLife.Core;

using Npgsql;

namespace GameOfLife.Api.Tests;

// The repository's SQL against a real PostgreSQL database, without the HTTP layer.
[Collection(PostgresFixture.CollectionName)]
public sealed class PostgresBoardRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    // Fails a test that would otherwise wait forever on a lock that was never released.
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    private NpgsqlDataSource? _dataSource;
    private PostgresBoardRepository? _repository;

    private CancellationToken Timeout => _timeout.Token;

    private PostgresBoardRepository Repository => _repository ?? throw new InvalidOperationException("Not initialized.");

    public async ValueTask InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync());
        _repository = new PostgresBoardRepository(_dataSource);
    }

    public async ValueTask DisposeAsync()
    {
        _timeout.Dispose();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    [Fact]
    public async Task Add_ThenFind_ReturnsTheSameBoard()
    {
        // 9 x 13 cells fill two 64-bit words, so the round trip crosses a word boundary.
        Board board = Board.Create(9, 13, (row, column) => ((row * 13) + column) % 3 == 0);

        StoredBoard added = await Repository.AddAsync(board, Timeout);
        StoredBoard? found = await Repository.FindAsync(added.Id, Timeout);

        Assert.Equal(added, found);
        Assert.Equal(0, added.Generation);
        Assert.Equal(BoardStatus.Active, added.Status);
    }

    [Fact]
    public async Task Find_UnknownId_ReturnsNull() =>
        Assert.Null(await Repository.FindAsync(Guid.NewGuid(), Timeout));
}
