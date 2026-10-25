using Microsoft.EntityFrameworkCore;
using Puzzle.Application.Interfaces;
using Puzzle.Domain.Entities;
using Puzzle.Infrastructure.Persistence;
using System.Text.Json;
namespace Puzzle.Infrastructure.Repositories;

public sealed class PuzzleRepository(PuzzleDbContext dbContext)
    : IPuzzleRepository
{
    public Task<Puzzle.Domain.Entities.Puzzle?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.Puzzles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == id,
                cancellationToken);

    public Task<Puzzle.Domain.Entities.Puzzle?> GetNextAsync(
        Guid? excludePuzzleId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Puzzles
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (excludePuzzleId.HasValue)
        {
            query = query.Where(p => p.Id != excludePuzzleId.Value);
        }

        // Avoid ORDER BY random()/random GUID over the entire puzzle table.
        // Count uses the active-puzzle index; a random offset is substantially
        // cheaper than sorting every eligible row for each request.
        return GetRandomAtOffsetAsync(query, cancellationToken);
    }

    private static async Task<Puzzle.Domain.Entities.Puzzle?> GetRandomAtOffsetAsync(
        IQueryable<Puzzle.Domain.Entities.Puzzle> query,
        CancellationToken cancellationToken)
    {
        var count = await query.CountAsync(cancellationToken);
        if (count == 0)
            return null;

        var offset = Random.Shared.Next(count);
        return await query
            .OrderBy(p => p.Rating)
            .ThenBy(p => p.Id)
            .Skip(offset)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Puzzle.Domain.Entities.Puzzle> Items, int TotalCount)>
        GetPageAsync(
            int page,
            int pageSize,
            int? minRating,
            int? maxRating,
            CancellationToken cancellationToken)
    {
        var query = dbContext.Puzzles
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (minRating.HasValue)
            query = query.Where(p => p.Rating >= minRating.Value);

        if (maxRating.HasValue)
            query = query.Where(p => p.Rating <= maxRating.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Rating)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> AddAttemptAsync(
        PuzzleAttempt attempt,
        int puzzleRating,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The unique (UserId, PuzzleId) key serializes simultaneous first attempts.
        // Backfill in the migration preserves existing users' already-awarded puzzles.
        var ratingAwardInserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO puzzle_rating_awards (""UserId"", ""PuzzleId"")
                VALUES ({attempt.UserId}, {attempt.PuzzleId})
                ON CONFLICT (""UserId"", ""PuzzleId"") DO NOTHING",
            cancellationToken);

        dbContext.PuzzleAttempts.Add(attempt);

        if (ratingAwardInserted == 1)
        {
            // Attempt, award marker, and integration event commit atomically.
            var payload = JsonSerializer.Serialize(new PuzzleAttemptedIntegrationEvent(
                attempt.Id,
                attempt.Id,
                attempt.UserId,
                attempt.PuzzleId,
                puzzleRating,
                attempt.IsCorrect,
                attempt.AttemptedAtUtc), JsonSerializerOptions.Web);

            dbContext.OutboxMessages.Add(new PuzzleOutboxMessage
            {
                EventId = attempt.Id,
                EventType = "puzzle.attempted",
                Payload = payload,
                CreatedAtUtc = attempt.AttemptedAtUtc
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ratingAwardInserted == 1;
    }

    private sealed record PuzzleAttemptedIntegrationEvent(
        Guid EventId,
        Guid AttemptId,
        Guid PlayerId,
        Guid PuzzleId,
        int PuzzleRating,
        bool IsCorrect,
        DateTime OccurredAtUtc);
}
