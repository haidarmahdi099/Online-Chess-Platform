namespace Game.Application.Events;

public sealed record GameAbortedEvent(
    Guid EventId,
    Guid GameId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    string Reason,
    DateTime OccurredAtUtc);
