namespace Tournament.Application.DTOs;

public sealed record TournamentStandingDto(
    int Rank,
    Guid UserId,
    decimal Points,
    int GamesPlayed,
    int Wins,
    int Draws,
    int Losses,
    int Byes,
    decimal Buchholz);

public sealed record TournamentStandingsDto(
    Guid TournamentId,
    string Status,
    int CurrentRound,
    int TotalRounds,
    IReadOnlyList<TournamentStandingDto> Standings);

public sealed record SetMatchResultRequest(string Result);
