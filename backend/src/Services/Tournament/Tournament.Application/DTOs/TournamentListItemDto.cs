namespace Tournament.Application.DTOs;

public sealed record TournamentListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    int InitialTimeSeconds,
    int IncrementSeconds,
    int ParticipantCount,
    int MaxParticipants,
    DateTime StartsAtUtc);
