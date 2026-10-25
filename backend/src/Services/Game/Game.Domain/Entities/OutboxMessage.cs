namespace Game.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(
    Guid id,
    string eventType,
    string payload)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Outbox message ID cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException(
                "Event type is required.",
                nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "Event payload is required.",
                nameof(payload));
        }

        return new OutboxMessage
        {
            Id = id,
            EventType = eventType.Trim(),
            Payload = payload,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    public void MarkPublished()
    {
        PublishedAtUtc = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;

        LastError = string.IsNullOrWhiteSpace(error)
            ? "Unknown publishing error."
            : error.Length > 2000
                ? error[..2000]
                : error;
    }
}