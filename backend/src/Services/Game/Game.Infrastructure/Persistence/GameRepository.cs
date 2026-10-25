using Game.Application.Exceptions;
using Game.Application.Games;
using Game.Domain.Entities;
using Game.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Game.Infrastructure.Persistence;

public sealed class GameRepository : IGameRepository
{
    private readonly GameDbContext _dbContext;

    public GameRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Game.Domain.Entities.Game?> GetByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Games
            .FirstOrDefaultAsync(
                game => game.Id == gameId,
                cancellationToken);
    }

    public Task<int> GetMoveCountAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return _dbContext.GameMoves
            .CountAsync(
                move => move.GameId == gameId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetFenHistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.GameMoves
            .AsNoTracking()
            .Where(move => move.GameId == gameId)
            .OrderBy(move => move.MoveNumber)
            .Select(move => move.FenAfterMove)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameMove>> GetMovesAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.GameMoves
            .AsNoTracking()
            .Where(move => move.GameId == gameId)
            .OrderBy(move => move.MoveNumber)
            .ToListAsync(cancellationToken);
    }

    public Task AddMoveAsync(
        GameMove move,
        CancellationToken cancellationToken)
    {
        return _dbContext.GameMoves
            .AddAsync(
                move,
                cancellationToken)
            .AsTask();
    }

    public async Task SaveChangesAsync(
    CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GameConcurrencyException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new GameMoveConflictException();
        }
    }

    public Task AddAsync(
    Game.Domain.Entities.Game game,
    CancellationToken cancellationToken)
    {
        return _dbContext.Games
            .AddAsync(game, cancellationToken)
            .AsTask();
    }

    public Task AddClockAsync(
        GameClock clock,
        CancellationToken cancellationToken)
    {
        return _dbContext.GameClocks
            .AddAsync(clock, cancellationToken)
            .AsTask();
    }

    public Task<GameClock?> GetClockByGameIdAsync(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        return _dbContext.GameClocks
            .FirstOrDefaultAsync(
                clock => clock.GameId == gameId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Game.Domain.Entities.Game>> GetGamesAsync(
    Guid? playerId,
    bool liveOnly,
    CancellationToken cancellationToken)
    {
        var query = _dbContext.Games
            .AsNoTracking()
            .AsQueryable();

        if (playerId.HasValue)
        {
            query = query.Where(game =>
                game.WhitePlayerId == playerId.Value ||
                game.BlackPlayerId == playerId.Value);
        }

        if (liveOnly)
        {
            query = query.Where(game =>
                game.Status == GameStatus.Live &&
                game.WhiteSpectatorConsent &&
                game.BlackSpectatorConsent &&
                !game.SpectatorConsentRejected);
        }

        return await query
            .OrderByDescending(game => game.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(
    IReadOnlyList<Game.Domain.Entities.Game> Items,
    int TotalCount)> GetGamesPageAsync(
    Guid? playerId,
    bool liveOnly,
    int page,
    int pageSize,
    CancellationToken cancellationToken)
    {
        var query = _dbContext.Games
            .AsNoTracking()
            .AsQueryable();

        if (playerId.HasValue)
        {
            query = query.Where(game =>
                game.WhitePlayerId == playerId.Value ||
                game.BlackPlayerId == playerId.Value);
        }

        if (liveOnly)
        {
            query = query.Where(game =>
                game.Status == GameStatus.Live &&
                game.WhiteSpectatorConsent &&
                game.BlackSpectatorConsent &&
                !game.SpectatorConsentRejected);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(game => game.CreatedAt)
            .ThenByDescending(game => game.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<GameChatMessage>> GetChatMessagesAsync(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.GameChatMessages
            .AsNoTracking()
            .Where(message => message.GameId == gameId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddChatMessageAsync(
    GameChatMessage message,
    CancellationToken cancellationToken)
    {
        return _dbContext.GameChatMessages
            .AddAsync(message, cancellationToken)
            .AsTask();
    }

    public async Task<GameStatsResponse> GetStatsAsync(
    Guid playerId,
    CancellationToken cancellationToken)
    {
        var games = await _dbContext.Games
            .AsNoTracking()
            .Where(game =>
                game.WhitePlayerId == playerId ||
                game.BlackPlayerId == playerId)
            .Select(game => new
            {
                game.Status,
                game.WinnerId,
                game.EndReason
            })
            .ToListAsync(cancellationToken);

        var totalGames = games.Count;

        var liveGames = games.Count(game =>
            game.Status == GameStatus.Live);

        var finishedGames = games.Count(game =>
            game.Status == GameStatus.Finished);

        var wins = games.Count(game =>
            game.Status == GameStatus.Finished &&
            game.WinnerId == playerId);

        var draws = games.Count(game =>
            game.Status == GameStatus.Finished &&
            game.WinnerId == null);

        var losses = games.Count(game =>
            game.Status == GameStatus.Finished &&
            game.WinnerId.HasValue &&
            game.WinnerId != playerId);

        return new GameStatsResponse(
            totalGames,
            liveGames,
            finishedGames,
            wins,
            losses,
            draws);
    }

    public Task AddOutboxMessageAsync(
    OutboxMessage message,
    CancellationToken cancellationToken)
    {
        return _dbContext.OutboxMessages
            .AddAsync(message, cancellationToken)
            .AsTask();
    }
}