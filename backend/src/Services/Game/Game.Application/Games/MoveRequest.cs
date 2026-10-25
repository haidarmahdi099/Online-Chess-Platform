namespace Game.Application.Games;

public sealed record MoveRequest(
    string From,
    string To,
    string? Promotion);