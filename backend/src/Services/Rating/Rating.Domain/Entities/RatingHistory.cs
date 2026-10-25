using Rating.Domain.Enums;

namespace Rating.Domain.Entities;

public sealed class RatingHistory
{
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }
    public RatingCategory Category { get; private set; }
    public int PreviousRating { get; private set; }
    public int NewRating { get; private set; }
    public int RatingChange => NewRating - PreviousRating;
    public string Reason { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RatingHistory() { }

    public RatingHistory(
        Guid playerId,
        RatingCategory category,
        int previousRating,
        int newRating,
        string reason,
        Guid eventId)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));

        if (eventId == Guid.Empty)
            throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (previousRating < 0 || newRating < 0)
            throw new ArgumentOutOfRangeException(nameof(newRating));

        Id = Guid.NewGuid();
        PlayerId = playerId;
        Category = category;
        PreviousRating = previousRating;
        NewRating = newRating;
        Reason = reason;
        EventId = eventId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
