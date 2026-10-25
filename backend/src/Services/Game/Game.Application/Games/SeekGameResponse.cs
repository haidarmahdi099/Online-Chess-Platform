namespace Matchmaking.Application.Contracts;

public sealed record SeekGameResponse(
    Guid SeekId,
    string Status,
    Guid? GameId);