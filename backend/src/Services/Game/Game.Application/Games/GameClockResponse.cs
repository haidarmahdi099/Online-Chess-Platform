
namespace Game.Application.Games;

public sealed record GameClockResponse(
    Guid GameId,
    long WhiteRemainingMilliseconds,
    long BlackRemainingMilliseconds,
    bool WhiteToMove,
    DateTime? TurnStartedAt);