namespace Game.Application.Events;

/// <summary>Raised when matchmaking pairs two seeks into a live game.</summary>
public sealed record GameMatchedEvent(
    Guid EventId,
    Guid GameId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    int InitialSeconds,
    int IncrementSeconds,
    string Category,
    bool Rated,
    DateTime OccurredAtUtc);
