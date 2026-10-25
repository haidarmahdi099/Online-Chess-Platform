namespace Puzzle.Infrastructure.Persistence;

/// <summary>Marks that a user/puzzle pair has already produced its one rating event.</summary>
public sealed class PuzzleRatingAward
{
    public Guid UserId { get; set; }
    public Guid PuzzleId { get; set; }
}
