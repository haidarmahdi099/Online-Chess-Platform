using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Realtime.Hubs;

public sealed record GameEndInfo(
    Guid GameId,
    Guid? WinnerId,
    string Result,
    string? EndReason,
    bool Rated);

/// <summary>
/// The only place that sends game:end / game:aborted. Both the hub (instant, after a player's
/// command) and the Kafka consumer (timeouts and anything that happened elsewhere) call it;
/// whichever arrives first announces, the other becomes a no-op, so clients get each outcome once.
/// State is in memory, which matches SignalR running without a backplane (single instance).
/// </summary>
public sealed class GameEndBroadcaster
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(15);

    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<GameEndBroadcaster> _logger;
    private readonly StackExchange.Redis.IConnectionMultiplexer? _redis;
    private readonly ConcurrentDictionary<Guid, DateTime> _announced = new();

    public GameEndBroadcaster(IHubContext<GameHub> hub, ILogger<GameEndBroadcaster> logger,
        StackExchange.Redis.IConnectionMultiplexer? redis = null)
    {
        _hub = hub;
        _logger = logger;
        _redis = redis;
    }

    public async Task<bool> AnnounceFinishedAsync(GameEndInfo info, CancellationToken cancellationToken = default)
    {
        var marker = await TryMarkAsync(info.GameId);
        if (marker is null)
            return false;

        try
        {
            await _hub.Clients
                .Groups(PlayerGroup(info.GameId), SpectatorGroup(info.GameId))
                .SendAsync(
                    "game:end",
                    new
                    {
                        info.GameId,
                        Type = ToClientType(info.EndReason),
                        Result = new { info.WinnerId, info.Result, info.EndReason, info.Rated }
                    },
                    cancellationToken);
            return true;
        }
        catch
        {
            // Let a retry (for example the Kafka redelivery) announce it.
            await ReleaseMarkAsync(info.GameId, marker);
            throw;
        }
    }

    public async Task<bool> AnnounceAbortedAsync(Guid gameId, string reason, CancellationToken cancellationToken = default)
    {
        var marker = await TryMarkAsync(gameId);
        if (marker is null)
            return false;

        try
        {
            await _hub.Clients
                .Groups(PlayerGroup(gameId), SpectatorGroup(gameId))
                .SendAsync(
                    "game:aborted",
                    new { GameId = gameId, Reason = reason },
                    cancellationToken);
            return true;
        }
        catch
        {
            await ReleaseMarkAsync(gameId, marker);
            throw;
        }
    }

    public static string ToClientType(string? endReason) => endReason switch
    {
        "Checkmate" => "checkmate",
        "Stalemate" => "stalemate",
        "Resignation" => "resignation",
        "Timeout" => "timeout",
        "DrawAgreement" or "ThreefoldRepetition" or "FiftyMoveRule" or "InsufficientMaterial" => "draw",
        _ => "finished"
    };

    private async Task<string?> TryMarkAsync(Guid gameId)
    {
        var marker = Guid.NewGuid().ToString("N");
        if (_redis is not null)
        {
            var key = $"realtime:game-end:{gameId:N}";
            var added = await _redis.GetDatabase().StringSetAsync(key, marker, Retention,
                StackExchange.Redis.When.NotExists);
            if (!added)
                _logger.LogDebug("Outcome of game {GameId} was already announced; skipping duplicate.", gameId);
            return added ? marker : null;
        }

        var now = DateTime.UtcNow;

        foreach (var entry in _announced)
        {
            if (now - entry.Value > Retention)
                _announced.TryRemove(entry.Key, out _);
        }

        var first = _announced.TryAdd(gameId, now);
        if (!first)
            _logger.LogDebug("Outcome of game {GameId} was already announced; skipping duplicate.", gameId);

        return first ? marker : null;
    }

    private async Task ReleaseMarkAsync(Guid gameId, string marker)
    {
        if (_redis is null)
        {
            _announced.TryRemove(gameId, out _);
            return;
        }

        const string compareAndDelete = """
            if redis.call('GET', KEYS[1]) == ARGV[1] then
              return redis.call('DEL', KEYS[1])
            end
            return 0
            """;
        await _redis.GetDatabase().ScriptEvaluateAsync(compareAndDelete,
            new StackExchange.Redis.RedisKey[] { $"realtime:game-end:{gameId:N}" },
            new StackExchange.Redis.RedisValue[] { marker });
    }

    private static string PlayerGroup(Guid gameId) => $"game:{gameId}";

    private static string SpectatorGroup(Guid gameId) => $"game-watch:{gameId}";
}
