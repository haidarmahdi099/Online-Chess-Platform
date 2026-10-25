namespace Game.Domain.Entities;

public sealed class GameClock
{
    public Guid Id { get; private set; }
    public uint Version { get; private set; }

    public Guid GameId { get; private set; }

    public long WhiteRemainingMilliseconds { get; private set; }

    public long BlackRemainingMilliseconds { get; private set; }

    public DateTime? TurnStartedAt { get; private set; }

    public bool WhiteToMove { get; private set; }

    private GameClock()
    {
    }

    private GameClock(
        Guid id,
        Guid gameId,
        long initialTimeMilliseconds)
    {
        Id = id;
        GameId = gameId;

        WhiteRemainingMilliseconds =
            initialTimeMilliseconds;

        BlackRemainingMilliseconds =
            initialTimeMilliseconds;

        WhiteToMove = true;
    }

    public static GameClock Create(
        Guid gameId,
        TimeSpan initialTime)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (initialTime <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Initial time must be greater than zero.",
                nameof(initialTime));
        }

        return new GameClock(
            Guid.NewGuid(),
            gameId,
            (long)initialTime.TotalMilliseconds);
    }

    public void StartTurn()
    {
        TurnStartedAt = DateTime.UtcNow;
    }

    public void CompleteTurn(TimeSpan increment)
    {
        if (TurnStartedAt is null)
            throw new InvalidOperationException(
                "The current turn has not started.");

        if (increment < TimeSpan.Zero)
            throw new ArgumentException(
                "Increment cannot be negative.",
                nameof(increment));

        var elapsed = DateTime.UtcNow - TurnStartedAt.Value;

        var elapsedMilliseconds = Math.Max(
            0,
            (long)elapsed.TotalMilliseconds);

        var incrementMilliseconds = (long)increment.TotalMilliseconds;

        if (WhiteToMove)
        {
            WhiteRemainingMilliseconds = Math.Max(
                0,
                WhiteRemainingMilliseconds - elapsedMilliseconds);

            WhiteRemainingMilliseconds += incrementMilliseconds;
        }
        else
        {
            BlackRemainingMilliseconds = Math.Max(
                0,
                BlackRemainingMilliseconds - elapsedMilliseconds);

            BlackRemainingMilliseconds += incrementMilliseconds;
        }

        WhiteToMove = !WhiteToMove;
        TurnStartedAt = DateTime.UtcNow;
    }

    public TimeSpan GetWhiteRemaining()
    {
        return GetRemaining(
            WhiteRemainingMilliseconds,
            true);
    }

    public TimeSpan GetBlackRemaining()
    {
        return GetRemaining(
            BlackRemainingMilliseconds,
            false);
    }

    private TimeSpan GetRemaining(
        long storedMilliseconds,
        bool white)
    {
        if (TurnStartedAt is null ||
            WhiteToMove != white)
        {
            return TimeSpan.FromMilliseconds(
                storedMilliseconds);
        }

        var elapsed =
            DateTime.UtcNow - TurnStartedAt.Value;

        var elapsedMilliseconds =
            Math.Max(
                0,
                (long)elapsed.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(
            Math.Max(
                0,
                storedMilliseconds -
                elapsedMilliseconds));
    }

    public void Stop()
    {
        if (TurnStartedAt is null)
            return;

        var remaining = GetCurrentPlayerRemaining();

        if (WhiteToMove)
        {
            WhiteRemainingMilliseconds =
                (long)remaining.TotalMilliseconds;
        }
        else
        {
            BlackRemainingMilliseconds =
                (long)remaining.TotalMilliseconds;
        }

        TurnStartedAt = null;
    }

    public bool IsWhiteTurn()
    {
        return WhiteToMove;
    }

    public bool IsBlackTurn()
    {
        return !WhiteToMove;
    }

    public bool HasTimedOut()
    {
        return GetCurrentPlayerRemaining() <= TimeSpan.Zero;
    }

    public TimeSpan GetCurrentPlayerRemaining()
    {
        return WhiteToMove
            ? GetWhiteRemaining()
            : GetBlackRemaining();
    }


}