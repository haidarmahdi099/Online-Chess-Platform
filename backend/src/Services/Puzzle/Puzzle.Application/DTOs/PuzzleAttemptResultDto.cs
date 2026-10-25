namespace Puzzle.Application.DTOs;

public sealed record PuzzleAttemptResultDto(
    Guid PuzzleId,
    bool IsCorrect,
    string Message,
    bool RatingAffected = true);