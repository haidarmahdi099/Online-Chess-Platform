using Rating.Domain.Enums;

namespace Rating.Domain.Entities;

public sealed class PlayerRating
{
    public const int DefaultRating = 1200;

    public Guid Id { get; private set; }
    // PostgreSQL xmin row version. EF uses this as an optimistic concurrency token so
    // parallel rating events cannot silently overwrite one another.
    public uint Version { get; private set; }
    public Guid PlayerId { get; private set; }
    public RatingCategory Category { get; private set; }
    public int Rating { get; private set; }
    public int GamesPlayed { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int Draws { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PlayerRating() { }

    public PlayerRating(Guid playerId, RatingCategory category, int initialRating = DefaultRating)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));

        if (initialRating < 0)
            throw new ArgumentOutOfRangeException(nameof(initialRating));

        Id = Guid.NewGuid();
        PlayerId = playerId;
        Category = category;
        Rating = initialRating;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void ApplyResult(int ratingChange, GameResult result)
    {
        Rating = Math.Max(0, Rating + ratingChange);

        switch (result)
        {
            case GameResult.Win:
                Wins++;
                GamesPlayed++;
                break;
            case GameResult.Loss:
                Losses++;
                GamesPlayed++;
                break;
            case GameResult.Draw:
                Draws++;
                GamesPlayed++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AdjustRating(int ratingChange)
    {
        Rating = Math.Max(0, Rating + ratingChange);
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
