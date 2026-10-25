namespace Game.Application.Games;

public sealed record GameActionResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);