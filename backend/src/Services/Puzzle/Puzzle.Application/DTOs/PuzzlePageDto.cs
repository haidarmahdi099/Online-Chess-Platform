namespace Puzzle.Application.DTOs;

public sealed record PuzzlePageDto(
    IReadOnlyList<PuzzleListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);