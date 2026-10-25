namespace Game.Application.Games;

public sealed record GameDetailsResponse(
    Guid Id,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    string Fen,
    string Status,
    Guid? WinnerId,
    string? EndReason,
    int InitialTimeSeconds,
    int IncrementSeconds,
    bool Rated,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt);