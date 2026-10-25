namespace Game.Application.Contracts;

public interface IGameSeekService
{
    Task<SeekGameResponse> CreateSeekAsync(
        Guid playerId,
        SeekGameRequest request,
        CancellationToken cancellationToken);

    Task<SeekGameResponse?> GetStatusAsync(
        Guid playerId,
        CancellationToken cancellationToken);

    Task CancelSeekAsync(
   Guid playerId,
   CancellationToken cancellationToken);
}
