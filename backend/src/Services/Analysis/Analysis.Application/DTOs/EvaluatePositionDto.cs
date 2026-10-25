namespace Analysis.Application.DTOs;

public sealed record EvaluatePositionDto(
    string Fen,
    int? Depth);
