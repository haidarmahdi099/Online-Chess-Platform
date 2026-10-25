using Tournament.Domain.Enums;

namespace Tournament.Application.Interfaces;

// Flat read model so the repository can compute participant counts in SQL.
public sealed record TournamentListRow(
    Guid Id,
    string Name,
    string? Description,
    TournamentStatus Status,
    int InitialTimeSeconds,
    int IncrementSeconds,
    int MaxParticipants,
    DateTime StartsAtUtc,
    int ParticipantCount);
