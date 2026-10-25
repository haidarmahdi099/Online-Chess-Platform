namespace Rating.Application.DTOs;

public sealed record LeaderboardEntry(
    Guid PlayerId,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    int Rank);
