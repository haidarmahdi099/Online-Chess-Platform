using Game.Application.Contracts;
using Game.Application.Events;
using Game.Application.Exceptions;
using Game.Domain.Entities;
using Game.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Game.Infrastructure.Persistence;

public sealed class GameSeekRepository : IGameSeekRepository
{
    private readonly GameDbContext _dbContext;

    public GameSeekRepository(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<GameSeek?> GetActiveByPlayerIdAsync(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.GameSeeks
            .FirstOrDefaultAsync(
                x => x.PlayerId == playerId && x.IsActive,
                cancellationToken);
    }

    public Task<GameSeek?> GetLatestByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken) =>
        _dbContext.GameSeeks
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

    public async Task AddAsync(
        GameSeek seek,
        CancellationToken cancellationToken)
    {
        await _dbContext.GameSeeks.AddAsync(seek, cancellationToken);
    }


    public async Task<Guid?> TryMatchAsync(GameSeek seek, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize matchmaking decisions for the same control so simultaneous
            // seekers cannot both miss each other and remain queued indefinitely.
            var matchKey = $"{seek.Rated}:{seek.Category}:{seek.InitialTimeSeconds}:{seek.IncrementSeconds}";
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({matchKey}))",
                cancellationToken);

            var staleSeeks = await _dbContext.GameSeeks
                .Where(x => x.IsActive && x.CreatedAt < DateTime.UtcNow.AddMinutes(-2))
                .ToListAsync(cancellationToken);
            foreach (var stale in staleSeeks)
                stale.Cancel();

            await _dbContext.GameSeeks.AddAsync(seek, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var candidates = await _dbContext.GameSeeks
                .FromSqlInterpolated($"""
                    SELECT * FROM game_seeks
                    WHERE "IsActive" = TRUE
                      AND "PlayerId" <> {seek.PlayerId}
                      AND "Rated" = {seek.Rated}
                      AND "Category" = {seek.Category}
                      AND "InitialTimeSeconds" = {seek.InitialTimeSeconds}
                      AND "IncrementSeconds" = {seek.IncrementSeconds}
                    ORDER BY "CreatedAt"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .ToListAsync(cancellationToken);
            var opponent = candidates.FirstOrDefault();

            if (opponent is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var whitePlayerId = Random.Shared.Next(2) == 0 ? seek.PlayerId : opponent.PlayerId;
            var blackPlayerId = whitePlayerId == seek.PlayerId ? opponent.PlayerId : seek.PlayerId;
            var timeControl = TimeControl.Create(seek.InitialTimeSeconds, seek.IncrementSeconds);
            var game = Game.Domain.Entities.Game.Create(whitePlayerId, blackPlayerId, timeControl, seek.Rated);
            game.Start();

            var clock = GameClock.Create(game.Id, timeControl.InitialTime);
            clock.StartTurn();
            opponent.Match(game.Id);
            seek.Match(game.Id);
            _dbContext.Games.Add(game);
            _dbContext.GameClocks.Add(clock);
            // Same transaction as the game, so a matched game is always announced (outbox pattern):
            // the player who was already waiting learns about it over SignalR instead of polling.
            _dbContext.OutboxMessages.Add(GameOutboxMessages.CreateMatched(game, seek.Category));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return game.Id;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new GameSeekConflictException();
        }
    }

    public async Task SaveChangesAsync(
    CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new GameSeekConflictException();
        }
    }
}