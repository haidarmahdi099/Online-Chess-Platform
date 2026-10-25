namespace Tournament.Infrastructure.Persistence;

public sealed class TournamentOutboxMessage
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public string DataJson { get; set; } = "null";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? LastError { get; set; }
}
