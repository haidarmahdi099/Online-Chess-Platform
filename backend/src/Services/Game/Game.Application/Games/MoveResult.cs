namespace Game.Application.Games;

public sealed record MoveResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string? Fen,
    bool IsCheck,
    bool IsCheckmate,
    bool IsStalemate,
    bool GameOver = false,
    string? EndReason = null,
    bool DrawOfferDeclined = false);