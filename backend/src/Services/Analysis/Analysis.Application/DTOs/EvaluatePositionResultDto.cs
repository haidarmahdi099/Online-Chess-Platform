namespace Analysis.Application.DTOs;

// ScoreType is "cp" (centipawns) or "mate" (moves to mate).
// Score is from White's perspective: positive favours White, negative Black.
public sealed record EvaluatePositionResultDto(
    string Fen,
    int Depth,
    string ScoreType,
    int Score,
    string? BestMove,
    string[] PrincipalVariation);
