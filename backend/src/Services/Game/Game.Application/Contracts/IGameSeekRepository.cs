using Game.Domain.Entities;

namespace Game.Application.Contracts;

public interface IGameSeekRepository
{
    Task<GameSeek?> GetActiveByPlayerIdAsync(
        Guid playerId,
        CancellationToken cancellationToken);

    Task<GameSeek?> GetLatestByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken);

    Task AddAsync(
        GameSeek seek,
        CancellationToken cancellationToken);

    Task<Guid?> TryMatchAsync(GameSeek seek, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);




}