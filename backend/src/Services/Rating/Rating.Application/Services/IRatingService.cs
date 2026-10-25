using Rating.Application.DTOs;
using Rating.Application.Events;
using Rating.Domain.Enums;

namespace Rating.Application.Services;

public interface IRatingService
{
    Task ProcessGameFinishedAsync(
        GameFinishedEvent gameEvent,
        CancellationToken cancellationToken = default);

    Task ProcessPuzzleAttemptedAsync(
        PuzzleAttemptedEvent puzzleEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Current rating in every category. Categories the player never played report the default rating.</summary>
    Task<PlayerRatingsResponse> GetPlayerRatingsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<RatingHistoryEntry>> GetRatingHistoryAsync(
        Guid playerId,
        RatingCategory category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        RatingCategory category,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<LeaderboardEntry>> GetLeaderboardPageAsync(
        RatingCategory category, int page, int pageSize, CancellationToken cancellationToken = default);
}
