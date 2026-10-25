using Moq;
using Rating.Application.Events;
using Rating.Application.Interfaces;
using Rating.Application.Services;
using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.UnitTests;

public sealed class RatingServiceTests
{
    private readonly Mock<IRatingRepository> _repository = new();
    private readonly Mock<IRatingCalculator> _calculator = new();
    private readonly RatingService _service;

    public RatingServiceTests() => _service = new RatingService(_repository.Object, _calculator.Object);

    [Fact]
    public async Task ProcessGameFinished_updates_legacy_and_time_control_categories_atomically()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        _calculator.Setup(c => c.CalculateChessRatingChange(1200, 1200, 1.0, 32)).Returns(16);
        _calculator.Setup(c => c.CalculateChessRatingChange(1200, 1200, 0.0, 32)).Returns(-16);
        IReadOnlyCollection<PlayerRating>? savedRatings = null;
        IReadOnlyCollection<RatingHistory>? savedHistory = null;
        _repository.Setup(r => r.SaveRatingChangesAsync(
                It.IsAny<IReadOnlyCollection<PlayerRating>>(), It.IsAny<IReadOnlyCollection<RatingHistory>>(),
                It.IsAny<Guid>(), "game.finished", It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<PlayerRating>, IReadOnlyCollection<RatingHistory>, Guid, string, CancellationToken>(
                (ratings, history, _, _, _) => { savedRatings = ratings; savedHistory = history; })
            .Returns(Task.CompletedTask);

        var gameEvent = new GameFinishedEvent(Guid.NewGuid(), Guid.NewGuid(), white, black,
            "WhiteWin", DateTime.UtcNow, InitialTimeSeconds: 300);
        await _service.ProcessGameFinishedAsync(gameEvent);

        Assert.NotNull(savedRatings);
        Assert.Equal(4, savedRatings.Count);
        Assert.Contains(savedRatings, r => r.PlayerId == white && r.Category == RatingCategory.Chess && r.Rating == 1216 && r.Wins == 1);
        Assert.Contains(savedRatings, r => r.PlayerId == black && r.Category == RatingCategory.Blitz && r.Rating == 1184 && r.Losses == 1);
        Assert.NotNull(savedHistory);
        Assert.Equal(4, savedHistory.Count);
        Assert.All(savedHistory, h => Assert.Equal(gameEvent.EventId, h.EventId));
        _repository.Verify(r => r.SaveRatingChangesAsync(It.IsAny<IReadOnlyCollection<PlayerRating>>(),
            It.IsAny<IReadOnlyCollection<RatingHistory>>(), gameEvent.EventId, "game.finished", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGameFinished_uses_one_update_when_category_is_legacy_chess()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        _calculator.Setup(c => c.CalculateChessRatingChange(1200, 1200, 0.5, 32)).Returns(0);
        IReadOnlyCollection<PlayerRating>? saved = null;
        _repository.Setup(r => r.SaveRatingChangesAsync(It.IsAny<IReadOnlyCollection<PlayerRating>>(),
                It.IsAny<IReadOnlyCollection<RatingHistory>>(), It.IsAny<Guid>(), "game.finished", It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<PlayerRating>, IReadOnlyCollection<RatingHistory>, Guid, string, CancellationToken>(
                (ratings, _, _, _, _) => saved = ratings)
            .Returns(Task.CompletedTask);

        await _service.ProcessGameFinishedAsync(new GameFinishedEvent(Guid.NewGuid(), Guid.NewGuid(), white, black,
            "Draw", DateTime.UtcNow, InitialTimeSeconds: 0));

        Assert.NotNull(saved);
        Assert.Equal(2, saved.Count);
        Assert.All(saved, r => Assert.Equal(RatingCategory.Chess, r.Category));
        Assert.All(saved, r => Assert.Equal(1, r.Draws));
    }

    [Fact]
    public async Task ProcessPuzzleAttempted_uses_provided_puzzle_difficulty_and_persists_history()
    {
        var playerId = Guid.NewGuid();
        var puzzleId = Guid.NewGuid();
        _calculator.Setup(c => c.CalculatePuzzleRatingChange(1200, 1700, true, 24)).Returns(12);
        PlayerRating? savedRating = null;
        RatingHistory? savedHistory = null;
        _repository.Setup(r => r.SaveRatingChangesAsync(It.IsAny<IReadOnlyCollection<PlayerRating>>(),
                It.IsAny<IReadOnlyCollection<RatingHistory>>(), It.IsAny<Guid>(), "puzzle.attempted", It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<PlayerRating>, IReadOnlyCollection<RatingHistory>, Guid, string, CancellationToken>(
                (ratings, history, _, _, _) => { savedRating = Assert.Single(ratings); savedHistory = Assert.Single(history); })
            .Returns(Task.CompletedTask);

        var evt = new PuzzleAttemptedEvent(Guid.NewGuid(), Guid.NewGuid(), playerId, puzzleId, true,
            DateTime.UtcNow, PuzzleRating: 1700);
        await _service.ProcessPuzzleAttemptedAsync(evt);

        Assert.NotNull(savedRating);
        Assert.Equal(1212, savedRating.Rating);
        Assert.Equal(RatingCategory.Puzzle, savedRating.Category);
        Assert.NotNull(savedHistory);
        Assert.Equal(1200, savedHistory.PreviousRating);
        Assert.Equal(1212, savedHistory.NewRating);
    }

    [Theory]
    [InlineData(0, "WhiteWin")]
    [InlineData(1, "SomethingElse")]
    [InlineData(2, "Draw")]
    public async Task ProcessGameFinished_rejects_invalid_event_identity_players_or_result(int invalidCase, string result)
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        if (invalidCase == 0) eventId = Guid.Empty;
        if (invalidCase == 1) black = white;
        if (invalidCase == 2) result = "Draw";
        if (invalidCase == 2) gameId = Guid.Empty;
        var evt = new GameFinishedEvent(eventId, gameId, white, black, result, DateTime.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ProcessGameFinishedAsync(evt));
        _repository.Verify(r => r.SaveRatingChangesAsync(It.IsAny<IReadOnlyCollection<PlayerRating>>(),
            It.IsAny<IReadOnlyCollection<RatingHistory>>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Player_without_ratings_gets_all_default_categories()
    {
        var playerId = Guid.NewGuid();
        _repository.Setup(r => r.GetPlayerRatingsAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PlayerRating>)Array.Empty<PlayerRating>());

        var result = await _service.GetPlayerRatingsAsync(playerId);

        Assert.Equal(playerId, result.PlayerId);
        Assert.Equal(1200, result.Chess.Rating);
        Assert.Equal(1200, result.Puzzle.Rating);
        Assert.Equal(1200, result.Bullet.Rating);
        Assert.Equal(1200, result.Blitz.Rating);
        Assert.Equal(1200, result.Rapid.Rating);
        Assert.Equal(1200, result.Classical.Rating);
    }
}
