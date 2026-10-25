namespace Game.Domain.Entities;

public sealed class GameSeek
{
    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public int InitialTimeSeconds { get; private set; }

    public int IncrementSeconds { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public bool Rated { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? MatchedGameId { get; private set; }

    private GameSeek()
    {
    }

    private GameSeek(
        Guid id,
        Guid playerId,
        int initialTimeSeconds,
        int incrementSeconds,
        string category,
        bool rated)
    {
        Id = id;
        PlayerId = playerId;
        InitialTimeSeconds = initialTimeSeconds;
        IncrementSeconds = incrementSeconds;
        Category = category;
        Rated = rated;
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public static GameSeek Create(
        Guid playerId,
        int initialTimeSeconds,
        int incrementSeconds,
        string category,
        bool rated)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException(
                "Player ID is required.",
                nameof(playerId));

        if (initialTimeSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(initialTimeSeconds));

        if (incrementSeconds < 0)
            throw new ArgumentOutOfRangeException(
                nameof(incrementSeconds));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException(
                "Category is required.",
                nameof(category));

        return new GameSeek(
            Guid.NewGuid(),
            playerId,
            initialTimeSeconds,
            incrementSeconds,
            category.Trim().ToLowerInvariant(),
            rated);
    }

    public void Match(Guid gameId)
    {
        if (!IsActive)
            throw new InvalidOperationException("Only an active search can be matched.");
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID is required.", nameof(gameId));

        MatchedGameId = gameId;
        IsActive = false;
    }

    public void Cancel()
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "This game search is no longer active.");

        IsActive = false;
    }
}