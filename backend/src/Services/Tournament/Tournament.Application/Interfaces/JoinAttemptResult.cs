using Tournament.Domain.Enums;

namespace Tournament.Application.Interfaces;

public sealed record JoinAttemptResult(
    JoinTournamentOutcome Outcome,
    int ParticipantCount);
