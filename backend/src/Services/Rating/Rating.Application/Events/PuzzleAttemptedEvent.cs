namespace Rating.Application.Events;

/// <summary>
/// PuzzleRating is optional for backward compatibility with events produced before
/// puzzle difficulty was included in the integration contract.
/// </summary>
public sealed record PuzzleAttemptedEvent(
    Guid EventId,
    Guid AttemptId,
    Guid PlayerId,
    Guid PuzzleId,
    bool IsCorrect,
    DateTime OccurredAtUtc,
    int? PuzzleRating = null);
