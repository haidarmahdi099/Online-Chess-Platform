using Puzzle.Domain.Entities;

namespace Puzzle.Application.Interfaces;

public interface IPuzzleRepository
{
    Task<Puzzle.Domain.Entities.Puzzle?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Puzzle.Domain.Entities.Puzzle?> GetNextAsync(
        Guid? excludePuzzleId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<Puzzle.Domain.Entities.Puzzle> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        int? minRating,
        int? maxRating,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores the attempt. Returns true when it was the user's first attempt at this puzzle and
    /// therefore produced a rating event; repeat attempts are stored but never rated.
    /// </summary>
    Task<bool> AddAttemptAsync(
        PuzzleAttempt attempt,
        int puzzleRating,
        CancellationToken cancellationToken);
}