namespace Tournament.Application.Interfaces;

public sealed record PendingProvision(
    Guid MatchId,
    Guid TournamentId,
    Guid WhitePlayerId,
    Guid BlackPlayerId,
    int InitialTimeSeconds,
    int IncrementSeconds);

public enum ProvisionOutcome
{
    Created,
    /// <summary>Network error, timeout, 5xx or a rejected service key: retry later.</summary>
    Transient,
    /// <summary>The Game service rejected the request (400/409): retrying cannot help.</summary>
    Permanent,
    /// <summary>GameService:BaseUrl or InternalServices:TournamentKey is missing.</summary>
    NotConfigured
}

public sealed record ProvisionResult(ProvisionOutcome Outcome, Guid? GameId = null, string? Error = null);

public interface IGameProvisioner
{
    /// <summary>
    /// Creates the live game for a match. The match id is the idempotency key, so calling
    /// this again for the same match returns the same game.
    /// </summary>
    Task<ProvisionResult> ProvisionAsync(PendingProvision match, CancellationToken cancellationToken);
}
