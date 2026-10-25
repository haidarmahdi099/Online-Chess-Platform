namespace Tournament.Application.DTOs;

public sealed record TournamentMatchDto(
    Guid Id,
    int RoundNumber,
    Guid WhitePlayerId,
    Guid? BlackPlayerId,
    Guid? GameId,
    string Status,
    string? Result = null,
    Guid? WinnerId = null);

public sealed record TournamentDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    DateTime StartsAtUtc,
    Guid CreatedByUserId,
    int InitialTimeSeconds,
    int IncrementSeconds,
    int MaxParticipants,
    IReadOnlyList<Guid> ParticipantIds,
    IReadOnlyList<TournamentMatchDto> Matches,
    int TotalRounds = 0,
    int CurrentRound = 0,
    DateTime? StartedAtUtc = null,
    DateTime? CompletedAtUtc = null);
