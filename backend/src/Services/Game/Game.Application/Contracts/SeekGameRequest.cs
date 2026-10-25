namespace Game.Application.Contracts;

public sealed record SeekGameRequest(
    TimeControlRequest TimeControl,
    bool Rated);

public sealed record TimeControlRequest(
    int InitialSeconds,
    int IncrementSeconds,
    string Category);