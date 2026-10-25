namespace Puzzle.Application.DTOs;

/// <summary>Public puzzle view; the returned FEN already includes the setup move.</summary>
public sealed record PuzzleDto(
    Guid Id,
    string Fen,
    string? InitialMove,
    int Rating,
    string[] Themes);
