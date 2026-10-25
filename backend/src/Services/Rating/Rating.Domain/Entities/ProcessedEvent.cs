namespace Rating.Domain.Entities;

public sealed class ProcessedEvent
{
    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; private set; }

    private ProcessedEvent()
    {
    }

    public ProcessedEvent(Guid eventId, string eventType)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));

        EventId = eventId;
        EventType = eventType;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
