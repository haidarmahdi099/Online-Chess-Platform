using Game.Domain.Entities;

namespace Game.Application.Games;

public interface IGameRepository
{
    Task<Game.Domain.Entities.Game?> GetByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    Task<int> GetMoveCountAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    /// <summary>FEN after every persisted move, oldest first (the initial position is not included).</summary>
    Task<IReadOnlyList<string>> GetFenHistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    /// <summary>All persisted moves of a game, oldest first.</summary>
    Task<IReadOnlyList<GameMove>> GetMovesAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Game.Domain.Entities.Game game,
        CancellationToken cancellationToken);

    Task AddClockAsync(
        GameClock clock,
        CancellationToken cancellationToken);

    Task AddMoveAsync(
        GameMove move,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<GameClock?> GetClockByGameIdAsync(
    Guid gameId,
    CancellationToken cancellationToken);

    Task<IReadOnlyList<Game.Domain.Entities.Game>> GetGamesAsync(
    Guid? playerId,
    bool liveOnly,
    CancellationToken cancellationToken);

    Task<(IReadOnlyList<Game.Domain.Entities.Game> Items, int TotalCount)>
    GetGamesPageAsync(
        Guid? playerId,
        bool liveOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameChatMessage>> GetChatMessagesAsync(
    Guid gameId,
    CancellationToken cancellationToken);

    Task AddChatMessageAsync(
        GameChatMessage message,
        CancellationToken cancellationToken);

    Task<GameStatsResponse> GetStatsAsync(
    Guid playerId,
    CancellationToken cancellationToken);

    Task AddOutboxMessageAsync(
    Game.Domain.Entities.OutboxMessage message,
    CancellationToken cancellationToken);
}