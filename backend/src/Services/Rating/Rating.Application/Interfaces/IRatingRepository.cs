using Rating.Domain.Entities;
using Rating.Domain.Enums;

namespace Rating.Application.Interfaces;

public interface IRatingRepository
{
    Task<PlayerRating?> GetPlayerRatingAsync(
        Guid playerId,
        RatingCategory category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerRating>> GetPlayerRatingsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RatingHistory> Items, int TotalCount)> GetHistoryPageAsync(
        Guid playerId,
        RatingCategory category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
        RatingCategory category,
        int limit,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PlayerRating> Items, int TotalCount)> GetLeaderboardPageAsync(
        RatingCategory category, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> IsEventProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task SaveRatingChangesAsync(
        IReadOnlyCollection<PlayerRating> ratings,
        IReadOnlyCollection<RatingHistory> history,
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default);
}
