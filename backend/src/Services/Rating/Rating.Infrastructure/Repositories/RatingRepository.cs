using System.Data;
using Microsoft.EntityFrameworkCore;
using Rating.Application.Interfaces;
using Rating.Application.Pagination;
using Rating.Domain.Entities;
using Rating.Domain.Enums;
using Rating.Infrastructure.Persistence;

namespace Rating.Infrastructure.Repositories;

public sealed class RatingRepository(
    RatingDbContext dbContext) : IRatingRepository
{
    public Task<PlayerRating?> GetPlayerRatingAsync(
        Guid playerId,
        RatingCategory category,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PlayerRatings
            .SingleOrDefaultAsync(
                rating => rating.PlayerId == playerId
                    && rating.Category == category,
                cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerRating>> GetPlayerRatingsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayerRatings
            .AsNoTracking()
            .Where(rating => rating.PlayerId == playerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<RatingHistory> Items, int TotalCount)> GetHistoryPageAsync(
        Guid playerId,
        RatingCategory category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var offset = PageWindow.GetOffset(page, pageSize);

        var query = dbContext.RatingHistories
            .AsNoTracking()
            .Where(history => history.PlayerId == playerId
                && history.Category == category);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(history => history.CreatedAtUtc)
            .ThenByDescending(history => history.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
        RatingCategory category,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "Leaderboard limit must be between 1 and 100.");

        return await dbContext.PlayerRatings
            .AsNoTracking()
            .Where(rating => rating.Category == category)
            .OrderByDescending(rating => rating.Rating)
            .ThenBy(rating => rating.PlayerId)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PlayerRating> Items, int TotalCount)> GetLeaderboardPageAsync(
        RatingCategory category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var offset = PageWindow.GetOffset(page, pageSize);
        var query = dbContext.PlayerRatings.AsNoTracking().Where(rating => rating.Category == category);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(rating => rating.Rating)
            .ThenBy(rating => rating.PlayerId)
            .Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<bool> IsEventProcessedAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ProcessedEvents
            .AnyAsync(
                processedEvent => processedEvent.EventId == eventId,
                cancellationToken);
    }

    public async Task SaveRatingChangesAsync(
        IReadOnlyCollection<PlayerRating> ratings,
        IReadOnlyCollection<RatingHistory> history,
        Guid eventId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException(
                "Event ID cannot be empty.", nameof(eventId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException(
                "Event type is required.", nameof(eventType));

        // Rating events for the same player can arrive on different Kafka partitions.
        // Serializable isolation prevents two transactions from both reading the same
        // rating and then overwriting each other's update. The consumer retries transient
        // serialization failures with a fresh DI scope/context.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var alreadyProcessed = await dbContext.ProcessedEvents
            .AnyAsync(
                processedEvent => processedEvent.EventId == eventId,
                cancellationToken);

        if (alreadyProcessed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        foreach (var rating in ratings)
        {
            var entry = dbContext.Entry(rating);
            if (entry.State == EntityState.Detached)
            {
                dbContext.PlayerRatings.Add(rating);
            }
        }
        await dbContext.RatingHistories.AddRangeAsync(history, cancellationToken);
        await dbContext.ProcessedEvents.AddAsync(
            new ProcessedEvent(eventId, eventType),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
