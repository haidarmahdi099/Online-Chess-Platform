using System.Text.Json.Serialization;
using Tournament.Domain.Enums;

namespace Tournament.Application.DTOs;

public sealed record JoinTournamentResultDto(
    Guid TournamentId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    JoinTournamentOutcome Outcome,
    bool Joined,
    string Message,
    int ParticipantCount);
