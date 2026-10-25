namespace Rating.Application.DTOs;

public sealed record PlayerRatingSummary(
    string Category, int Rating, int GamesPlayed, int Wins, int Losses, int Draws);

public sealed record PlayerRatingsResponse(
    Guid PlayerId,
    PlayerRatingSummary Chess,
    PlayerRatingSummary Puzzle,
    PlayerRatingSummary Bullet,
    PlayerRatingSummary Blitz,
    PlayerRatingSummary Rapid,
    PlayerRatingSummary Classical);
