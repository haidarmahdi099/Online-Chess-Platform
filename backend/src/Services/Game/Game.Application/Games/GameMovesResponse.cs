namespace Game.Application.Games;

/// <param name="Ply">1-based half-move index (1 = White's first move).</param>
/// <param name="MoveNumber">Full-move number as written in a score sheet (1, 1, 2, 2, ...).</param>
/// <param name="Color">"white" or "black".</param>
/// <param name="Fen">Position after the move, so a client can replay by jumping to any ply.</param>
public sealed record MoveHistoryItem(
    int Ply,
    int MoveNumber,
    string Color,
    string From,
    string To,
    string? Promotion,
    string San,
    string Fen,
    DateTime PlayedAt);

public sealed record GameMovesResponse(
    Guid GameId,
    string InitialFen,
    string Status,
    IReadOnlyList<MoveHistoryItem> Moves);
