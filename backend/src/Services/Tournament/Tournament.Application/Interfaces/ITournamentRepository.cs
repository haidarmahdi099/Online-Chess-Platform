using Tournament.Domain.Entities;
using Tournament.Domain.Enums;

namespace Tournament.Application.Interfaces;

public interface ITournamentRepository
{
    Task<(IReadOnlyList<TournamentListRow> Items, int TotalCount)> GetPageAsync(int page, int pageSize, TournamentStatus? status, CancellationToken cancellationToken);
    Task<JoinAttemptResult?> JoinAsync(Guid tournamentId, Guid userId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<ChessTournament?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken);
    Task<ChessTournament> CreateAsync(ChessTournament tournament, CancellationToken cancellationToken);
    Task<bool> CanManageAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid tournamentId, CancellationToken cancellationToken);
    Task<bool> LeaveAsync(Guid tournamentId, Guid userId, DateTime nowUtc, CancellationToken cancellationToken);

    // ---- Lifecycle. Each call is one database transaction that holds the tournament row lock. ----

    /// <summary>
    /// Upcoming -> InProgress and round 1 are created in ONE transaction. With
    /// <paramref name="cancelIfUnderfilled"/> a tournament with fewer than two players is cancelled.
    /// </summary>
    Task<StartTournamentOutcome> StartAsync(Guid tournamentId, DateTime nowUtc, bool cancelIfUnderfilled, CancellationToken cancellationToken);

    /// <summary>If every match of the current round is settled, pairs the next round or completes the tournament.</summary>
    Task<RoundAdvanceOutcome> AdvanceAsync(Guid tournamentId, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Records a finished game (matchId == gameId). Idempotent; advances the round when it was the last game.</summary>
    Task<MatchResultOutcome> RecordGameResultAsync(Guid matchId, string result, DateTime finishedAtUtc, CancellationToken cancellationToken);

    /// <summary>Owner/admin override for a stuck match.</summary>
    Task<MatchResultOutcome> ForceMatchResultAsync(Guid tournamentId, Guid matchId, string result, DateTime nowUtc, CancellationToken cancellationToken);

    Task<CompleteTournamentOutcome> CompleteAsync(Guid tournamentId, DateTime nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetDueUpcomingIdsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

    /// <summary>In-progress tournaments whose current round is fully settled but was never advanced.</summary>
    Task<IReadOnlyList<Guid>> GetStalledTournamentIdsAsync(int take, CancellationToken cancellationToken);

    // ---- Durable game provisioning ----

    Task<IReadOnlyList<PendingProvision>> GetPendingProvisionsAsync(Guid? tournamentId, DateTime nowUtc, int take, CancellationToken cancellationToken);
    Task<bool> AttachGameAsync(Guid tournamentId, Guid matchId, Guid gameId, CancellationToken cancellationToken);
    Task RecordProvisionFailureAsync(Guid matchId, string error, bool permanent, DateTime nowUtc, CancellationToken cancellationToken);
    /// <summary>Puts ProvisionFailed matches back in the queue. Returns how many were reset.</summary>
    Task<int> ResetFailedProvisionsAsync(Guid tournamentId, CancellationToken cancellationToken);
}
