namespace Game.Application.Games;

public sealed record GameStatsResponse(
    int TotalGames,
    int LiveGames,
    int FinishedGames,
    int Wins,
    int Losses,
    int Draws);