using Rating.Application.DTOs;
using Rating.Application.Events;
using Rating.Application.Interfaces;
using Rating.Application.Pagination;
using Rating.Domain.Entities;
using Rating.Domain.Enums;
namespace Rating.Application.Services;

public sealed class RatingService(
    IRatingRepository repository,
    IRatingCalculator calculator) : IRatingService
{
    public async Task ProcessGameFinishedAsync(
        GameFinishedEvent gameEvent,
        CancellationToken cancellationToken = default)
    {
        ValidateEventId(gameEvent.EventId);
        if (gameEvent.GameId == Guid.Empty) throw new ArgumentException("Game ID cannot be empty.");
        if (gameEvent.WhitePlayerId == Guid.Empty || gameEvent.BlackPlayerId == Guid.Empty) throw new ArgumentException("Both player IDs are required.");
        if (gameEvent.WhitePlayerId == gameEvent.BlackPlayerId) throw new ArgumentException("Players must be different.");

        var (whiteScore, blackScore, whiteResult, blackResult) = gameEvent.Result switch
        {
            "WhiteWin" => (1.0, 0.0, GameResult.Win, GameResult.Loss),
            "BlackWin" => (0.0, 1.0, GameResult.Loss, GameResult.Win),
            "Draw" => (0.5, 0.5, GameResult.Draw, GameResult.Draw),
            _ => throw new ArgumentException($"Unsupported game result: {gameEvent.Result}")
        };

        var category = MapTimeControlCategory(gameEvent.InitialTimeSeconds);
        // Continue updating the legacy Chess rating for existing clients while also maintaining
        // separate time-control ratings. Both category updates share the same idempotent event.
        var categories = new[] { RatingCategory.Chess, category }.Distinct().ToArray();
        var ratingsToSave = new List<PlayerRating>();
        var history = new List<RatingHistory>();
        foreach (var ratingCategory in categories)
        {
            var whiteRating = await repository.GetPlayerRatingAsync(gameEvent.WhitePlayerId, ratingCategory, cancellationToken)
                ?? new PlayerRating(gameEvent.WhitePlayerId, ratingCategory);
            var blackRating = await repository.GetPlayerRatingAsync(gameEvent.BlackPlayerId, ratingCategory, cancellationToken)
                ?? new PlayerRating(gameEvent.BlackPlayerId, ratingCategory);
            var whitePrevious = whiteRating.Rating;
            var blackPrevious = blackRating.Rating;
            var whiteChange = calculator.CalculateChessRatingChange(whitePrevious, blackPrevious, whiteScore);
            var blackChange = calculator.CalculateChessRatingChange(blackPrevious, whitePrevious, blackScore);
            whiteRating.ApplyResult(whiteChange, whiteResult);
            blackRating.ApplyResult(blackChange, blackResult);
            ratingsToSave.AddRange([whiteRating, blackRating]);
            history.Add(new RatingHistory(gameEvent.WhitePlayerId, ratingCategory, whitePrevious, whiteRating.Rating, $"Game:{gameEvent.GameId}:{whiteResult}", gameEvent.EventId));
            history.Add(new RatingHistory(gameEvent.BlackPlayerId, ratingCategory, blackPrevious, blackRating.Rating, $"Game:{gameEvent.GameId}:{blackResult}", gameEvent.EventId));
        }
        await repository.SaveRatingChangesAsync(ratingsToSave, history, gameEvent.EventId, "game.finished", cancellationToken);
    }

    public static RatingCategory MapTimeControlCategory(int initialTimeSeconds) => initialTimeSeconds switch
    {
        <= 0 => RatingCategory.Chess, // Backward-compatible handling for historical events without time control.
        < 180 => RatingCategory.Bullet,
        < 600 => RatingCategory.Blitz,
        < 1800 => RatingCategory.Rapid,
        _ => RatingCategory.Classical
    };

    public async Task ProcessPuzzleAttemptedAsync(
        PuzzleAttemptedEvent puzzleEvent,
        CancellationToken cancellationToken = default)
    {
        ValidateEventId(puzzleEvent.EventId);

        if (puzzleEvent.PlayerId == Guid.Empty)
            throw new ArgumentException("Player ID cannot be empty.");

        if (puzzleEvent.PuzzleId == Guid.Empty)
            throw new ArgumentException("Puzzle ID cannot be empty.");

        var playerRating = await repository.GetPlayerRatingAsync(
            puzzleEvent.PlayerId,
            RatingCategory.Puzzle,
            cancellationToken);

        playerRating ??= new PlayerRating(
            puzzleEvent.PlayerId, RatingCategory.Puzzle);

        var previousRating = playerRating.Rating;

        // New events carry the actual puzzle difficulty. Fall back to the player's
        // current rating only for older messages already present in Kafka.
        var puzzleRating = puzzleEvent.PuzzleRating ?? previousRating;
        if (puzzleRating < 0)
            throw new ArgumentOutOfRangeException(nameof(puzzleEvent.PuzzleRating));

        var ratingChange = calculator.CalculatePuzzleRatingChange(
            previousRating,
            puzzleRating,
            puzzleEvent.IsCorrect);

        playerRating.AdjustRating(ratingChange);

        var history = new[]
        {
            new RatingHistory(
                puzzleEvent.PlayerId,
                RatingCategory.Puzzle,
                previousRating,
                playerRating.Rating,
                $"Puzzle:{puzzleEvent.PuzzleId}:{(puzzleEvent.IsCorrect ? "Correct" : "Incorrect")}",
                puzzleEvent.EventId)
        };

        await repository.SaveRatingChangesAsync(
            [playerRating],
            history,
            puzzleEvent.EventId,
            "puzzle.attempted",
            cancellationToken);
    }

    private static void ValidateEventId(Guid eventId)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Event ID cannot be empty.");
    }

    public async Task<PlayerRatingsResponse> GetPlayerRatingsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var ratings = await repository.GetPlayerRatingsAsync(
            playerId, cancellationToken);

        PlayerRatingSummary Summarize(RatingCategory category)
        {
            var rating = ratings.FirstOrDefault(r => r.Category == category);

            return rating is null
                ? new PlayerRatingSummary(
                    category.ToString().ToLowerInvariant(),
                    PlayerRating.DefaultRating, 0, 0, 0, 0)
                : new PlayerRatingSummary(
                    category.ToString().ToLowerInvariant(),
                    rating.Rating,
                    rating.GamesPlayed,
                    rating.Wins,
                    rating.Losses,
                    rating.Draws);
        }

        return new PlayerRatingsResponse(
            playerId,
            Summarize(RatingCategory.Chess),
            Summarize(RatingCategory.Puzzle),
            Summarize(RatingCategory.Bullet),
            Summarize(RatingCategory.Blitz),
            Summarize(RatingCategory.Rapid),
            Summarize(RatingCategory.Classical));
    }

    public async Task<PagedResponse<RatingHistoryEntry>> GetRatingHistoryAsync(
        Guid playerId,
        RatingCategory category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await repository.GetHistoryPageAsync(
            playerId, category, page, pageSize, cancellationToken);

        return new PagedResponse<RatingHistoryEntry>(
            items
                .Select(history => new RatingHistoryEntry(
                    history.PreviousRating,
                    history.NewRating,
                    history.NewRating - history.PreviousRating,
                    history.Reason,
                    history.CreatedAtUtc))
                .ToList(),
            page,
            pageSize,
            total);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        RatingCategory category,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var ratings = await repository.GetLeaderboardAsync(
            category, limit, cancellationToken);

        return ratings
            .Select((rating, index) => new LeaderboardEntry(
                rating.PlayerId,
                rating.Rating,
                rating.GamesPlayed,
                rating.Wins,
                rating.Losses,
                rating.Draws,
                index + 1))
            .ToList();
    }

    public async Task<PagedResponse<LeaderboardEntry>> GetLeaderboardPageAsync(
        RatingCategory category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (ratings, total) = await repository.GetLeaderboardPageAsync(category, page, pageSize, cancellationToken);
        var offset = PageWindow.GetOffset(page, pageSize);
        var entries = ratings.Select((rating, index) => new LeaderboardEntry(
            rating.PlayerId, rating.Rating, rating.GamesPlayed, rating.Wins, rating.Losses, rating.Draws, offset + index + 1)).ToList();
        return new PagedResponse<LeaderboardEntry>(entries, page, pageSize, total);
    }

}
