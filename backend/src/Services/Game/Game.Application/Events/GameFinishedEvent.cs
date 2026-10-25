namespace Game.Application.Events;

public sealed record GameFinishedEvent(
    Guid EventId,
    Guid GameId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    string Result,
    DateTime OccurredAtUtc,
    bool Rated = true,
    string? EndReason = null,
    Guid? WinnerId = null,
    int InitialTimeSeconds = 0,
    int IncrementSeconds = 0);
