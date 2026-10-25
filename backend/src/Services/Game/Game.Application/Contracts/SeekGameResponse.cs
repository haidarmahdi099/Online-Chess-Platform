namespace Game.Application.Contracts;

public sealed record SeekGameResponse(
    Guid SeekId,
    string Status,
    int InitialSeconds,
    int IncrementSeconds,
    string Category,
    bool Rated,
    DateTime CreatedAt,
    Guid? GameId = null);