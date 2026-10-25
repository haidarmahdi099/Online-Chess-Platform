using Puzzle.Application.DTOs;

namespace Puzzle.Application.Services;

public interface IPuzzleService
{
    Task<PuzzleDto?> GetNextAsync(
        Guid? excludePuzzleId,
        CancellationToken cancellationToken);

    Task<PuzzlePageDto> GetPuzzlesAsync(
        int page,
        int pageSize,
        int? minRating,
        int? maxRating,
        CancellationToken cancellationToken);

    /// <summary>
    /// Step-by-step play. <paramref name="userMoves"/> are only the user's moves so far (including
    /// the one just played). Returns continue + the opponent's reply, solved, or failed (with the
    /// solution). The attempt is stored, and rated once per user and puzzle, only when it ends.
    /// </summary>
    Task<PuzzleStepResultDto?> SubmitStepAsync(
        Guid puzzleId,
        Guid userId,
        string[] userMoves,
        CancellationToken cancellationToken);

    Task<PuzzleAttemptResultDto?> SubmitAttemptAsync(
        Guid puzzleId,
        Guid userId,
        string[] moves,
        CancellationToken cancellationToken);
}