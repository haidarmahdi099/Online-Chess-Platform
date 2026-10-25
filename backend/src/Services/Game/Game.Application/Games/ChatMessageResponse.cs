namespace Game.Application.Games;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid UserId,
    string Message,
    DateTime CreatedAt);