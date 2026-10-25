using Realtime.Hubs;

namespace Realtime.UnitTests;

public sealed class GamePresenceTrackerTests
{
    [Fact]
    public void Join_tracks_unique_users_and_multiple_connections()
    {
        var tracker = new GamePresenceTracker();

        var first = tracker.Join("game:match-1", "user-a", "connection-1");
        var secondTab = tracker.Join("game:match-1", "user-a", "connection-2");
        var opponent = tracker.Join("game:match-1", "user-b", "connection-3");

        Assert.Equal(1, first.CurrentCount);
        Assert.Equal(1, secondTab.CurrentCount);
        Assert.Equal(2, opponent.CurrentCount);
        Assert.Equal(("game:match-1", "user-a"), tracker.GetConnection("connection-2"));
    }

    [Fact]
    public void Leave_keeps_user_online_until_last_connection_leaves()
    {
        var tracker = new GamePresenceTracker();
        tracker.Join("game:match-1", "user-a", "connection-1");
        tracker.Join("game:match-1", "user-a", "connection-2");
        tracker.Join("game:match-1", "user-b", "connection-3");

        var firstLeave = tracker.Leave("connection-1");
        var lastLeave = tracker.Leave("connection-2");

        Assert.Equal(("game:match-1", 2), firstLeave);
        Assert.Equal(("game:match-1", 1), lastLeave);
        Assert.Null(tracker.GetConnection("connection-2"));
    }

    [Fact]
    public void Joining_another_game_moves_connection_and_removes_old_presence()
    {
        var tracker = new GamePresenceTracker();
        tracker.Join("game:match-1", "user-a", "connection-1");

        var moved = tracker.Join("game:match-2", "user-a", "connection-1");

        Assert.Equal("game:match-1", moved.PreviousGroup);
        Assert.Equal(0, moved.PreviousCount);
        Assert.Equal(1, moved.CurrentCount);
        Assert.Equal(("game:match-2", "user-a"), tracker.GetConnection("connection-1"));
    }

    [Fact]
    public async Task Track_operations_use_local_counts_without_redis()
    {
        var tracker = new GamePresenceTracker();
        tracker.Join("game:match-1", "user-a", "connection-1");

        Assert.Equal(1, await tracker.TrackJoinAsync("game:match-1", "user-a", "connection-1", 1));
        Assert.Equal(0, await tracker.TrackLeaveAsync("game:match-1", "connection-1", 0));
    }

    [Fact]
    public async Task RefreshLeases_is_safe_without_redis()
    {
        var tracker = new GamePresenceTracker();
        tracker.Join("game:match-1", "user-a", "connection-1");

        await tracker.RefreshLeasesAsync(CancellationToken.None);
    }
}
