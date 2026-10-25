namespace Puzzle.Infrastructure.Persistence;

/// <summary>
/// Durable integration event persisted in the same database transaction as the puzzle attempt.
/// The EventId is stable so downstream consumers can safely deduplicate redelivery.
/// </summary>
public sealed class PuzzleOutboxMessage
{
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
