namespace Puzzle.Domain.Entities;

public sealed class Puzzle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Fen { get; set; }

    // UCI moves separated by spaces, for example: "e2e4 e7e5".
    // Keep this private to the service; never expose it in public DTOs.
    public required string SolutionMoves { get; set; }

    public int Rating { get; set; } = 1500;

    public string Themes { get; set; } = "";

    public string? Source { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PuzzleAttempt> Attempts { get; set; } =
        new List<PuzzleAttempt>();

    public string? ExternalPuzzleId { get; set; }

}
