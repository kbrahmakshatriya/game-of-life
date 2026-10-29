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
    public async Task FindAndLock_UnknownId_ReturnNull()
    {
        Assert.Null(await Repository.FindAsync(Guid.NewGuid(), Timeout));
        Assert.Null(await Repository.LockAsync(Guid.NewGuid(), Timeout));
    }

    [Fact]
    public async Task Lock_ThenSave_ReplacesTheBoard()
    {
        StoredBoard added = await Repository.AddAsync(Board.Parse(TestBoards.VerticalBlinker), Timeout);
        StoredBoard settled = added with
        {
            Generation = 7,
            Status = BoardStatus.Oscillator,
            Period = 2,
            Board = Board.Parse(TestBoards.HorizontalBlinker),
        };

        await using (IBoardLock? locked = await Repository.LockAsync(added.Id, Timeout))
        {
            Assert.NotNull(locked);
            Assert.Equal(added, locked.Board);
            await locked.SaveAsync(settled, Timeout);
        }

        Assert.Equal(settled, await Repository.FindAsync(added.Id, Timeout));
    }

    [Fact]
    public async Task Lock_DisposedWithoutSaving_LeavesTheBoardAsItWasAndReleasesIt()
    {
        StoredBoard added = await Repository.AddAsync(Board.Parse(TestBoards.Block), Timeout);

        await using (IBoardLock? locked = await Repository.LockAsync(added.Id, Timeout))
        {
            Assert.NotNull(locked);
        }

        // Released, so the lock can be taken again at once instead of waiting until the timeout fails the test.
        await using IBoardLock? again = await Repository.LockAsync(added.Id, Timeout);
        Assert.Equal(added, again?.Board);
    }

    [Fact]
    public async Task Table_RejectsAnActiveBoardWithAPeriod()
    {
        StoredBoard added = await Repository.AddAsync(Board.Parse(TestBoards.Block), Timeout);
        await using IBoardLock? locked = await Repository.LockAsync(added.Id, Timeout);
        Assert.NotNull(locked);

        // The table's CHECK constraint keeps period set exactly when the board has settled.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => locked.SaveAsync(added with { Period = 3 }, Timeout));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }
}
