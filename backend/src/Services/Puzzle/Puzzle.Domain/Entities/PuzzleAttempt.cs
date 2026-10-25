namespace Puzzle.Domain.Entities;

public sealed class PuzzleAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PuzzleId { get; set; }

    public Guid UserId { get; set; }

    public required string SubmittedMoves { get; set; }

    public bool IsCorrect { get; set; }

    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;

    public Puzzle Puzzle { get; set; } = null!;
}