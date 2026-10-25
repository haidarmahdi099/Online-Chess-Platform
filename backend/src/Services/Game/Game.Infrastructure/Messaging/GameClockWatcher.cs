using Game.Application.Events;
using Game.Domain.Enums;
using Game.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Messaging;

public sealed class GameClockWatcher : BackgroundService
{
    // A game that never started (only reachable through the disabled direct-creation path)
    // is void after this long.
    private static readonly TimeSpan WaitingGameTimeout = TimeSpan.FromSeconds(60);
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameClockWatcher> _logger;

    public GameClockWatcher(IServiceScopeFactory scopeFactory, ILogger<GameClockWatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game clock watcher iteration failed.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var now = DateTime.UtcNow;

        var staleWaitingGames = await db.Games
            .Where(g => g.Status == GameStatus.Waiting && g.CreatedAt < now.Subtract(WaitingGameTimeout))
            .OrderBy(g => g.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var game in staleWaitingGames)
        {
            try
            {
                game.AbortWaitingGame();
                db.OutboxMessages.Add(GameOutboxMessages.CreateAborted(game, "WaitingTimeout"));
            }
            catch (InvalidOperationException)
            {
                // Someone started or aborted it in the meantime.
            }
        }

        // Cheap scan: read only the clock columns of running games, decide in memory which have
        // expired, and only then load (and track) those few games.
        var runningClocks = await (
                from clockRow in db.GameClocks.AsNoTracking()
                join gameRow in db.Games.AsNoTracking() on clockRow.GameId equals gameRow.Id
                where gameRow.Status == GameStatus.Live && clockRow.TurnStartedAt != null
                select new
                {
                    clockRow.GameId,
                    clockRow.TurnStartedAt,
                    clockRow.WhiteToMove,
                    clockRow.WhiteRemainingMilliseconds,
                    clockRow.BlackRemainingMilliseconds
                })
            .ToListAsync(cancellationToken);

        var expiredGameIds = runningClocks
            .Where(c =>
            {
                var remaining = c.WhiteToMove
                    ? c.WhiteRemainingMilliseconds
                    : c.BlackRemainingMilliseconds;
                var elapsed = Math.Max(0, (long)(now - c.TurnStartedAt!.Value).TotalMilliseconds);
                return remaining - elapsed <= 0;
            })
            .Select(c => c.GameId)
            .Take(BatchSize)
            .ToArray();

        if (expiredGameIds.Length > 0)
        {
            var expiredGames = await db.Games
                .Where(g => expiredGameIds.Contains(g.Id) && g.Status == GameStatus.Live)
                .ToListAsync(cancellationToken);
            var clocks = await db.GameClocks
                .Where(c => expiredGameIds.Contains(c.GameId))
                .ToDictionaryAsync(c => c.GameId, cancellationToken);

            foreach (var game in expiredGames)
            {
                if (!clocks.TryGetValue(game.Id, out var clock) || !clock.HasTimedOut())
                    continue;

                try
                {
                    var timedOutPlayerId = clock.IsWhiteTurn() ? game.WhitePlayerId : game.BlackPlayerId;
                    clock.Stop();
                    game.FinishByTimeout(timedOutPlayerId);

                    var moveCount = await db.GameMoves.CountAsync(m => m.GameId == game.Id, cancellationToken);
                    if (game.Rated && moveCount < 4)
                    {
                        game.AbortShortRatedGame();
                        db.OutboxMessages.Add(GameOutboxMessages.CreateAborted(game, "RatedGameTooShort"));
                        continue;
                    }

                    // Same factory as the move path: identical payload, stable event id, WinnerId set.
                    db.OutboxMessages.Add(GameOutboxMessages.CreateFinished(game));
                }
                catch (InvalidOperationException)
                {
                    // A concurrent move may have already finished the game.
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another instance or a move won the optimistic concurrency race.
            _logger.LogDebug(ex, "A game changed while the clock watcher was processing; it will be rechecked.");
            db.ChangeTracker.Clear();
        }
    }
}
