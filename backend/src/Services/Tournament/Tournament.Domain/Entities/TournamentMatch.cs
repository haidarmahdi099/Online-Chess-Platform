namespace Tournament.Domain.Entities;

public static class MatchStatuses
{
    /// <summary>Paired, waiting for the Game service to create the live game.</summary>
    public const string Scheduled = "Scheduled";
    /// <summary>Live game exists and is being played.</summary>
    public const string InProgress = "InProgress";
    public const string Finished = "Finished";
    public const string Bye = "Bye";
    /// <summary>The Game service permanently rejected the game; needs a retry or a forced result.</summary>
    public const string ProvisionFailed = "ProvisionFailed";
}

public static class MatchResults
{
    public const string WhiteWin = "WhiteWin";
    public const string BlackWin = "BlackWin";
    public const string Draw = "Draw";
    public const string Bye = "Bye";

    public static bool IsPlayableResult(string? result) =>
        result is WhiteWin or BlackWin or Draw;
}

public sealed class TournamentMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public int RoundNumber { get; set; }
    public Guid WhitePlayerId { get; set; }
    public Guid? BlackPlayerId { get; set; }
    public Guid? GameId { get; set; }
    public string Status { get; set; } = MatchStatuses.Scheduled;

    // WhiteWin / BlackWin / Draw / Bye once the match is settled.
    public string? Result { get; set; }
    public Guid? WinnerId { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    // Durable game-provisioning state (retry with backoff).
    public int ProvisionAttempts { get; set; }
    public DateTime? NextProvisionAtUtc { get; set; }
    public string? LastProvisionError { get; set; }

    public ChessTournament Tournament { get; set; } = null!;

    public bool IsSettled =>
        Status == MatchStatuses.Finished || Status == MatchStatuses.Bye;

    public bool NeedsGame =>
        Status == MatchStatuses.Scheduled && BlackPlayerId.HasValue && !GameId.HasValue;

    public void Settle(string result, DateTime finishedAtUtc)
    {
        if (!MatchResults.IsPlayableResult(result))
            throw new ArgumentException("Unknown match result.", nameof(result));

        Result = result;
        WinnerId = result switch
        {
            MatchResults.WhiteWin => WhitePlayerId,
            MatchResults.BlackWin => BlackPlayerId,
            _ => null
        };
        Status = MatchStatuses.Finished;
        FinishedAtUtc = finishedAtUtc;
        NextProvisionAtUtc = null;
    }
}
