namespace Rating.Application.DTOs;

/// <param name="Reason">e.g. "Game:{gameId}:Win" or "Puzzle:{puzzleId}:Correct".</param>
public sealed record RatingHistoryEntry(
    int PreviousRating,
    int NewRating,
    int Change,
    string Reason,
    DateTime CreatedAtUtc);
