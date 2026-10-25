namespace Tournament.Domain.Pairing;

public static class TournamentFormat
{
    public const int MinPlayers = 2;

    /// <summary>
    /// Swiss round count: ceil(log2(players)), but never more than players - 1
    /// (so a no-rematch pairing always exists) and at least 1.
    /// </summary>
    public static int RoundsFor(int playerCount)
    {
        if (playerCount < MinPlayers)
            throw new ArgumentOutOfRangeException(nameof(playerCount), "At least two players are required.");

        var rounds = (int)Math.Ceiling(Math.Log2(playerCount));
        return Math.Clamp(rounds, 1, playerCount - 1);
    }
}
