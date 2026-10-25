namespace Puzzle.Application.DTOs;

/// <param name="Status">"continue", "solved" or "failed".</param>
/// <param name="OpponentReply">UCI reply to play on the board when Status is "continue".</param>
/// <param name="Solution">Remaining solution line, only revealed when Status is "failed".</param>
/// <param name="RatingAffected">False for mid-puzzle steps and for repeat attempts of the same puzzle.</param>
public sealed record PuzzleStepResultDto(
    Guid PuzzleId,
    string Status,
    string? OpponentReply,
    string[]? Solution,
    string Message,
    bool RatingAffected);
