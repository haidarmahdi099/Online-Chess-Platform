using Game.Domain.Entities;

namespace Game.UnitTests.Domain;

public sealed class GameClockTests
{
    [Fact]
    public void Create_initializes_both_sides_and_white_to_move()
    {
        var gameId = Guid.NewGuid();
        var clock = GameClock.Create(gameId, TimeSpan.FromMinutes(5));

        Assert.Equal(gameId, clock.GameId);
        Assert.Equal(TimeSpan.FromMinutes(5), clock.GetWhiteRemaining());
        Assert.Equal(TimeSpan.FromMinutes(5), clock.GetBlackRemaining());
        Assert.True(clock.IsWhiteTurn());
        Assert.False(clock.IsBlackTurn());
    }

    [Fact]
    public void Create_rejects_empty_game_id_and_zero_time()
    {
        Assert.Throws<ArgumentException>(() => GameClock.Create(Guid.Empty, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentException>(() => GameClock.Create(Guid.NewGuid(), TimeSpan.Zero));
    }

    [Fact]
    public void Complete_turn_requires_started_clock_and_non_negative_increment()
    {
        var clock = GameClock.Create(Guid.NewGuid(), TimeSpan.FromMinutes(1));

        Assert.Throws<InvalidOperationException>(() => clock.CompleteTurn(TimeSpan.Zero));
        clock.StartTurn();
        Assert.Throws<ArgumentException>(() => clock.CompleteTurn(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Complete_turn_switches_side_and_applies_increment_to_mover()
    {
        var clock = GameClock.Create(Guid.NewGuid(), TimeSpan.FromMinutes(1));
        clock.StartTurn();

        clock.CompleteTurn(TimeSpan.FromSeconds(2));

        Assert.True(clock.IsBlackTurn());
        Assert.True(clock.GetWhiteRemaining() <= TimeSpan.FromSeconds(62));
        Assert.True(clock.GetWhiteRemaining() > TimeSpan.FromSeconds(60));
        Assert.True(clock.GetBlackRemaining() <= TimeSpan.FromMinutes(1));
        Assert.True(clock.GetBlackRemaining() > TimeSpan.FromMinutes(1) - TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Stop_freezes_current_clock()
    {
        var clock = GameClock.Create(Guid.NewGuid(), TimeSpan.FromMinutes(1));
        clock.StartTurn();
        Thread.Sleep(20);

        clock.Stop();
        var stopped = clock.GetWhiteRemaining();
        Thread.Sleep(20);

        Assert.Equal(stopped, clock.GetWhiteRemaining());
        Assert.False(clock.HasTimedOut());
    }
}
