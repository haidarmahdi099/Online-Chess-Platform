using Game.Application.Contracts;
using Game.Application.Exceptions;
using Game.Domain.Entities;
using Moq;

namespace Game.UnitTests.Application;

public sealed class GameSeekServiceTests
{
    private readonly Mock<IGameSeekRepository> _repository = new();
    private readonly GameSeekService _service;

    public GameSeekServiceTests()
    {
        _service = new GameSeekService(_repository.Object);
    }

    [Fact]
    public async Task CreateSeek_returns_searching_when_repository_does_not_match()
    {
        var playerId = Guid.NewGuid();
        _repository.Setup(r => r.TryMatchAsync(It.IsAny<GameSeek>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var response = await _service.CreateSeekAsync(
            playerId,
            new SeekGameRequest(new TimeControlRequest(300, 0, "blitz"), Rated: true),
            CancellationToken.None);

        Assert.Equal("Searching", response.Status);
        Assert.Null(response.GameId);
        Assert.Equal(300, response.InitialSeconds);
        Assert.Equal("blitz", response.Category);
        Assert.True(response.Rated);
        _repository.Verify(r => r.TryMatchAsync(
            It.Is<GameSeek>(s => s.PlayerId == playerId && s.IsActive && s.Rated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSeek_returns_matched_game_id_when_repository_matches()
    {
        var matchedGameId = Guid.NewGuid();
        _repository.Setup(r => r.TryMatchAsync(It.IsAny<GameSeek>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchedGameId);

        var response = await _service.CreateSeekAsync(
            Guid.NewGuid(),
            new SeekGameRequest(new TimeControlRequest(600, 0, "rapid"), Rated: false),
            CancellationToken.None);

        Assert.Equal("Matched", response.Status);
        Assert.Equal(matchedGameId, response.GameId);
        Assert.False(response.Rated);
    }

    [Theory]
    [InlineData(0, 0, "blitz")]
    [InlineData(300, -1, "blitz")]
    [InlineData(300, 0, "rapid")]
    [InlineData(300, 0, "unknown")]
    public async Task CreateSeek_rejects_invalid_time_control_without_repository_call(
        int initialSeconds,
        int incrementSeconds,
        string category)
    {
        var request = new SeekGameRequest(
            new TimeControlRequest(initialSeconds, incrementSeconds, category),
            Rated: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSeekAsync(Guid.NewGuid(), request, CancellationToken.None));

        _repository.Verify(r => r.TryMatchAsync(It.IsAny<GameSeek>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStatus_reports_searching_for_latest_active_seek()
    {
        var playerId = Guid.NewGuid();
        var seek = GameSeek.Create(playerId, 300, 0, "blitz", rated: false);
        _repository.Setup(r => r.GetLatestByPlayerIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seek);

        var response = await _service.GetStatusAsync(playerId, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Searching", response.Status);
        Assert.Equal(seek.Id, response.SeekId);
        Assert.Null(response.GameId);
    }

    [Fact]
    public async Task CancelSeek_cancels_active_seek_and_saves()
    {
        var seek = GameSeek.Create(Guid.NewGuid(), 300, 0, "blitz", rated: false);
        _repository.Setup(r => r.GetActiveByPlayerIdAsync(seek.PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seek);

        await _service.CancelSeekAsync(seek.PlayerId, CancellationToken.None);

        Assert.False(seek.IsActive);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelSeek_throws_not_found_when_player_has_no_active_seek()
    {
        _repository.Setup(r => r.GetActiveByPlayerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSeek?)null);

        await Assert.ThrowsAsync<GameSeekNotFoundException>(() =>
            _service.CancelSeekAsync(Guid.NewGuid(), CancellationToken.None));

        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
