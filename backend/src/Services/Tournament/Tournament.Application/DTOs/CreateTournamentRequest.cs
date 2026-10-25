namespace Tournament.Application.DTOs;

public sealed record CreateTournamentRequest(string Name, string? Description, DateTime StartsAtUtc, int InitialTimeSeconds = 180, int IncrementSeconds = 0, int MaxParticipants = 16);
