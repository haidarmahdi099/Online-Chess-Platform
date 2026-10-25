namespace Game.Application.Games;

public sealed record CreateGameRequest(
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    int InitialTimeSeconds,
    int IncrementSeconds,
    bool Rated = false);