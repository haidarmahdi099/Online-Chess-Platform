using Game.Application.Exceptions;
using Game.Application.Validation;
using Game.Domain.Entities;

namespace Game.Application.Contracts;

public sealed class GameSeekService : IGameSeekService
{
    private readonly IGameSeekRepository _repository;

    public GameSeekService(IGameSeekRepository repository)
    {
        _repository = repository;
    }

    public async Task<SeekGameResponse> CreateSeekAsync(
        Guid playerId,
        SeekGameRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TimeControl is null ||
            !TimeControlValidator.IsValid(
                request.TimeControl.InitialSeconds,
                request.TimeControl.IncrementSeconds,
                request.TimeControl.Category))
        {
            throw new ArgumentException("The time control is invalid.");
        }

        var seek = GameSeek.Create(
            playerId,
            request.TimeControl.InitialSeconds,
            request.TimeControl.IncrementSeconds,
            request.TimeControl.Category,
            request.Rated);

        var gameId = await _repository.TryMatchAsync(seek, cancellationToken);

        return new SeekGameResponse(
            seek.Id,
            gameId.HasValue ? "Matched" : "Searching",
            seek.InitialTimeSeconds,
            seek.IncrementSeconds,
            seek.Category,
            seek.Rated,
            seek.CreatedAt,
            gameId);
    }


    public async Task<SeekGameResponse?> GetStatusAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var seek = await _repository.GetLatestByPlayerIdAsync(playerId, cancellationToken);
        if (seek is null)
            return null;

        var status = seek.IsActive ? "Searching" :
            seek.MatchedGameId.HasValue ? "Matched" : "CancelledOrExpired";
        if (seek.IsActive && seek.CreatedAt < DateTime.UtcNow.AddMinutes(-2))
        {
            seek.Cancel();
            await _repository.SaveChangesAsync(cancellationToken);
            status = "Expired";
        }

        return new SeekGameResponse(seek.Id, status, seek.InitialTimeSeconds,
            seek.IncrementSeconds, seek.Category, seek.Rated, seek.CreatedAt, seek.MatchedGameId);
    }

    public async Task CancelSeekAsync(
    Guid playerId,
    CancellationToken cancellationToken)
    {
        var activeSeek = await _repository.GetActiveByPlayerIdAsync(
            playerId,
            cancellationToken);

        if (activeSeek is null)
        {
            throw new GameSeekNotFoundException();
        }

        activeSeek.Cancel();

        await _repository.SaveChangesAsync(cancellationToken);
    }
}