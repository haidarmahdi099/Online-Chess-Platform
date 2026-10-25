namespace Tournament.Application.DTOs;

public sealed record TournamentPageDto(
    IReadOnlyList<TournamentListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
