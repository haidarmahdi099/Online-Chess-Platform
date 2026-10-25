using Microsoft.AspNetCore.SignalR;

namespace Realtime.Hubs;

public sealed record GameMatchInfo(
    Guid GameId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    int InitialSeconds,
    int IncrementSeconds,
    string Category,
    bool Rated);

/// <summary>
/// Sends game:matched to both players the moment matchmaking pairs them, so the lobby does not
/// have to poll GET /games/seek/status. Delivery is per user (not per game group) because the
/// players have not joined the game yet.
/// </summary>
public sealed class GameMatchBroadcaster
{
    private readonly IHubContext<GameHub> _hub;

    public GameMatchBroadcaster(IHubContext<GameHub> hub)
    {
        _hub = hub;
    }

    public Task AnnounceMatchedAsync(GameMatchInfo info, CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Users(new[] { info.WhitePlayerId.ToString(), info.BlackPlayerId.ToString() })
            .SendAsync(
                "game:matched",
                new
                {
                    info.GameId,
                    info.WhitePlayerId,
                    info.BlackPlayerId,
                    info.InitialSeconds,
                    info.IncrementSeconds,
                    info.Category,
                    info.Rated
                },
                cancellationToken);
}
