namespace Puzzle.Application.DTOs;

public sealed record PuzzleListItemDto(
    Guid Id,
    int Rating,
    string[] Themes);