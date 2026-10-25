
namespace Realtime.Hubs;

public sealed class GamePresenceTracker
{
    private readonly StackExchange.Redis.IConnectionMultiplexer? _redis;
    private readonly object _lock = new();

    private readonly Dictionary<string, (string GroupName, string UserId)>
        _connectionGames = new();

    private readonly Dictionary<string, Dictionary<string, HashSet<string>>>
        _gameUsers = new();

    public GamePresenceTracker(StackExchange.Redis.IConnectionMultiplexer? redis = null) => _redis = redis;

    public (string? PreviousGroup, int? PreviousCount, int CurrentCount)
        Join(string groupName, string userId, string connectionId)
    {
        lock (_lock)
        {
            string? previousGroup = null;
            int? previousCount = null;

            if (_connectionGames.TryGetValue(connectionId, out var existing))
            {
                previousGroup = existing.GroupName;
                RemoveLocked(connectionId, existing);

                if (_gameUsers.TryGetValue(previousGroup, out var previousUsers))
                    previousCount = previousUsers.Count;
                else
                    previousCount = 0;
            }

            if (!_gameUsers.TryGetValue(groupName, out var users))
            {
                users = new Dictionary<string, HashSet<string>>();
                _gameUsers[groupName] = users;
            }

            if (!users.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                users[userId] = connections;
            }

            connections.Add(connectionId);
            _connectionGames[connectionId] = (groupName, userId);

            return (previousGroup, previousCount, users.Count);
        }
    }

    public (string GroupName, int OnlineUsers)? Leave(string connectionId)
    {
        lock (_lock)
        {
            if (!_connectionGames.TryGetValue(connectionId, out var info))
                return null;

            RemoveLocked(connectionId, info);

            var count = _gameUsers.TryGetValue(info.GroupName, out var users)
                ? users.Count
                : 0;

            return (info.GroupName, count);
        }
    }

    public (string GroupName, string UserId)? GetConnection(string connectionId)
    {
        lock (_lock)
        {
            return _connectionGames.TryGetValue(connectionId, out var info)
                ? info
                : null;
        }
    }

    /// <summary>
    /// Updates the cross-instance count. The local maps still authorize commands for this
    /// instance's connections; Redis makes the user count consistent across SignalR nodes.
    /// </summary>
    public async Task<int> TrackJoinAsync(string groupName, string userId, string connectionId, int localCount)
    {
        if (_redis is null || !TryGetGameId(groupName, out var gameId))
            return localCount;

        const string script = """
            local expired = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[3])
            for _, connectionId in ipairs(expired) do
              redis.call('ZREM', KEYS[1], connectionId)
              redis.call('HDEL', KEYS[2], connectionId)
            end
            redis.call('ZADD', KEYS[1], ARGV[4], ARGV[1])
            redis.call('HSET', KEYS[2], ARGV[1], ARGV[2])
            redis.call('PEXPIRE', KEYS[1], 86400000)
            redis.call('PEXPIRE', KEYS[2], 86400000)
            local users = {}
            local values = redis.call('HVALS', KEYS[2])
            for _, userId in ipairs(values) do users[userId] = true end
            local count = 0
            for _ in pairs(users) do count = count + 1 end
            return count
            """;
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(script, PresenceKeys(gameId),
            new StackExchange.Redis.RedisValue[]
            {
                connectionId,
                userId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeMilliseconds()
            });
        return (int)result;
    }

    public async Task<int> TrackLeaveAsync(string groupName, string connectionId, int localCount)
    {
        if (_redis is null || !TryGetGameId(groupName, out var gameId))
            return localCount;

        const string script = """
            local expired = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[2])
            for _, expiredConnectionId in ipairs(expired) do
              redis.call('ZREM', KEYS[1], expiredConnectionId)
              redis.call('HDEL', KEYS[2], expiredConnectionId)
            end
            redis.call('ZREM', KEYS[1], ARGV[1])
            redis.call('HDEL', KEYS[2], ARGV[1])
            if redis.call('ZCARD', KEYS[1]) == 0 then
              redis.call('DEL', KEYS[1], KEYS[2])
              return 0
            end
            local users = {}
            local values = redis.call('HVALS', KEYS[2])
            for _, userId in ipairs(values) do users[userId] = true end
            local count = 0
            for _ in pairs(users) do count = count + 1 end
            return count
            """;
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(script, PresenceKeys(gameId),
            new StackExchange.Redis.RedisValue[]
            {
                connectionId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        return (int)result;
    }

    public async Task RefreshLeasesAsync(CancellationToken cancellationToken)
    {
        if (_redis is null)
            return;

        KeyValuePair<string, (string GroupName, string UserId)>[] connections;
        lock (_lock)
            connections = _connectionGames.ToArray();

        var db = _redis.GetDatabase();
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeMilliseconds();
        foreach (var game in connections.GroupBy(entry => entry.Value.GroupName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetGameId(game.Key, out var gameId))
                continue;

            var keys = PresenceKeys(gameId);
            var entries = game.Select(entry => new StackExchange.Redis.SortedSetEntry(
                entry.Key, expiresAt)).ToArray();
            foreach (var entry in entries)
            {
                const string refreshScript = """
                    if redis.call('HEXISTS', KEYS[2], ARGV[1]) == 1 then
                      redis.call('ZADD', KEYS[1], ARGV[2], ARGV[1])
                      redis.call('PEXPIRE', KEYS[1], 86400000)
                      redis.call('PEXPIRE', KEYS[2], 86400000)
                    end
                    """;
                await db.ScriptEvaluateAsync(refreshScript, keys,
                    new StackExchange.Redis.RedisValue[] { entry.Element, entry.Score });
            }
        }
    }

    private static StackExchange.Redis.RedisKey[] PresenceKeys(Guid gameId)
    {
        var tag = $"{{{gameId:N}}}";
        return new StackExchange.Redis.RedisKey[]
        {
            $"realtime:presence:{tag}:leases",
            $"realtime:presence:{tag}:connections"
        };
    }

    private static bool TryGetGameId(string groupName, out Guid gameId)
    {
        gameId = Guid.Empty;
        return groupName.StartsWith("game:", StringComparison.Ordinal) &&
               Guid.TryParse(groupName["game:".Length..], out gameId);
    }

    private void RemoveLocked(
        string connectionId,
        (string GroupName, string UserId) info)
    {
        _connectionGames.Remove(connectionId);

        if (!_gameUsers.TryGetValue(info.GroupName, out var users))
            return;

        if (users.TryGetValue(info.UserId, out var connections))
        {
            connections.Remove(connectionId);

            if (connections.Count == 0)
                users.Remove(info.UserId);
        }

        if (users.Count == 0)
            _gameUsers.Remove(info.GroupName);
    }
}
