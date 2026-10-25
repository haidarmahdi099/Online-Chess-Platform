namespace Game.Domain.Chess;

public sealed record ChessMoveResult(
    bool IsLegal,
    string? ErrorCode,
    string? ErrorMessage,
    string? NewFen,
    bool IsCheck,
    bool IsCheckmate,
    bool IsStalemate);