using Tournament.Application.DTOs;
using Tournament.Application.Interfaces;
using Tournament.Domain.Enums;

namespace Tournament.Application.Services;

public sealed record ProvisionBatchResult(int Created, int Failed, bool NotConfigured);

public interface ITournamentService
{
    Task<TournamentPageDto> GetTournamentsAsync(int page, int pageSize, TournamentStatus? status, CancellationToken cancellationToken);
    Task<JoinTournamentResultDto?> JoinAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken);
    Task<TournamentDetailsDto?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken);
    Task<TournamentStandingsDto?> GetStandingsAsync(Guid tournamentId, CancellationToken cancellationToken);
    Task<TournamentDetailsDto> CreateAsync(CreateTournamentRequest request, Guid createdByUserId, CancellationToken cancellationToken);
    Task<bool> CanManageAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid tournamentId, CancellationToken cancellationToken);
    Task<bool> LeaveAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Starts the tournament and creates round 1 atomically, then provisions the games.</summary>
    Task<StartTournamentOutcome> StartAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>Pairs the next round (or completes the tournament) when the current round is done, and retries pending game creation.</summary>
    Task<RoundAdvanceOutcome> AdvanceAsync(Guid tournamentId, CancellationToken cancellationToken);

    Task<CompleteTournamentOutcome> CompleteAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>Applied by the game-finished consumer. Idempotent.</summary>
    Task<MatchResultOutcome> RecordGameResultAsync(Guid gameId, string result, DateTime finishedAtUtc, CancellationToken cancellationToken);

    Task<MatchResultOutcome> ForceMatchResultAsync(Guid tournamentId, Guid matchId, string result, CancellationToken cancellationToken);

    /// <summary>Re-queues ProvisionFailed matches and tries them immediately.</summary>
    Task<int> RetryFailedProvisionsAsync(Guid tournamentId, CancellationToken cancellationToken);

    Task<ProvisionBatchResult> ProvisionPendingAsync(Guid? tournamentId, CancellationToken cancellationToken);

    /// <summary>One background cycle: auto-start due tournaments, advance stalled rounds, create pending games.</summary>
    Task RunMaintenanceAsync(CancellationToken cancellationToken);
}
