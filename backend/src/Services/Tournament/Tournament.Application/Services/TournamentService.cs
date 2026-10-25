using Microsoft.Extensions.Logging;
using Tournament.Application.DTOs;
using Tournament.Application.Interfaces;
using Tournament.Domain.Entities;
using Tournament.Domain.Enums;
using Tournament.Domain.Pairing;

namespace Tournament.Application.Services;

public sealed class TournamentService(
    ITournamentRepository repository,
    IGameProvisioner gameProvisioner,
    ILogger<TournamentService> logger)
    : ITournamentService
{
    private const int ProvisionBatchSize = 50;

    public async Task<TournamentPageDto> GetTournamentsAsync(
        int page,
        int pageSize,
        TournamentStatus? status,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.GetPageAsync(
            page, pageSize, status, cancellationToken);

        return new TournamentPageDto(
            items.Select(t => new TournamentListItemDto(
                t.Id,
                t.Name,
                t.Description,
                t.Status.ToString(),
                t.InitialTimeSeconds,
                t.IncrementSeconds,
                t.ParticipantCount,
                t.MaxParticipants,
                t.StartsAtUtc)).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<JoinTournamentResultDto?> JoinAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attempt = await repository.JoinAsync(
            tournamentId, userId, DateTime.UtcNow, cancellationToken);

        if (attempt is null)
            return null;

        return new JoinTournamentResultDto(
            tournamentId,
            attempt.Outcome,
            attempt.Outcome == JoinTournamentOutcome.Joined,
            MessageFor(attempt.Outcome),
            attempt.ParticipantCount);
    }

    private static string MessageFor(JoinTournamentOutcome outcome) =>
        outcome switch
        {
            JoinTournamentOutcome.Joined => "Joined the tournament.",
            JoinTournamentOutcome.AlreadyJoined => "You have already joined this tournament.",
            JoinTournamentOutcome.TournamentFull => "The tournament is full.",
            JoinTournamentOutcome.RegistrationClosed => "Registration for this tournament is closed.",
            _ => "Unable to join the tournament."
        };

    public async Task<TournamentDetailsDto?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var t = await repository.GetByIdAsync(tournamentId, cancellationToken);
        return t is null ? null : Map(t);
    }

    public async Task<TournamentStandingsDto?> GetStandingsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var t = await repository.GetByIdAsync(tournamentId, cancellationToken);
        if (t is null) return null;

        var rows = StandingsCalculator.Compute(t.Participants.Select(p => p.UserId), t.Matches);
        var standings = rows
            .Select((row, index) => new TournamentStandingDto(
                index + 1,
                row.UserId,
                row.Points,
                row.GamesPlayed,
                row.Wins,
                row.Draws,
                row.Losses,
                row.Byes,
                row.Buchholz))
            .ToList();

        return new TournamentStandingsDto(t.Id, t.Status.ToString(), t.CurrentRound, t.TotalRounds, standings);
    }

    public async Task<TournamentDetailsDto> CreateAsync(CreateTournamentRequest request, Guid createdByUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120) throw new ArgumentException("Name is required and must be at most 120 characters.");
        if (request.Description?.Length > 1000) throw new ArgumentException("Description must be at most 1000 characters.");
        if (request.StartsAtUtc.Kind == DateTimeKind.Unspecified || request.StartsAtUtc <= DateTime.UtcNow) throw new ArgumentException("StartsAtUtc must be a future UTC date/time.");
        if (request.InitialTimeSeconds is < 10 or > 7200) throw new ArgumentException("InitialTimeSeconds must be between 10 and 7200.");
        if (request.IncrementSeconds is < 0 or > 300) throw new ArgumentException("IncrementSeconds must be between 0 and 300.");
        if (request.MaxParticipants is < 2 or > 256) throw new ArgumentException("MaxParticipants must be between 2 and 256.");
        var tournament = await repository.CreateAsync(new ChessTournament { CreatedByUserId = createdByUserId, Name = request.Name.Trim(), Description = request.Description?.Trim(), StartsAtUtc = request.StartsAtUtc.ToUniversalTime(), InitialTimeSeconds = request.InitialTimeSeconds, IncrementSeconds = request.IncrementSeconds, MaxParticipants = request.MaxParticipants }, cancellationToken);
        return Map(tournament);
    }

    public Task<bool> CanManageAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken) => repository.CanManageAsync(tournamentId, userId, cancellationToken);

    public Task<bool> CancelAsync(Guid tournamentId, CancellationToken cancellationToken) => repository.CancelAsync(tournamentId, cancellationToken);
    public Task<bool> LeaveAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken) => repository.LeaveAsync(tournamentId, userId, DateTime.UtcNow, cancellationToken);

    // ------------------------------------------------------------------ lifecycle

    public async Task<StartTournamentOutcome> StartAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var outcome = await repository.StartAsync(tournamentId, DateTime.UtcNow, cancelIfUnderfilled: false, cancellationToken);
        if (outcome == StartTournamentOutcome.Started)
            await TryProvisionAsync(tournamentId, cancellationToken);
        return outcome;
    }

    public async Task<RoundAdvanceOutcome> AdvanceAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var outcome = await repository.AdvanceAsync(tournamentId, DateTime.UtcNow, cancellationToken);
        if (outcome is RoundAdvanceOutcome.NextRoundCreated or RoundAdvanceOutcome.RoundInProgress)
            await TryProvisionAsync(tournamentId, cancellationToken);
        return outcome;
    }

    public Task<CompleteTournamentOutcome> CompleteAsync(Guid tournamentId, CancellationToken cancellationToken) =>
        repository.CompleteAsync(tournamentId, DateTime.UtcNow, cancellationToken);

    public async Task<MatchResultOutcome> RecordGameResultAsync(
        Guid gameId,
        string result,
        DateTime finishedAtUtc,
        CancellationToken cancellationToken)
    {
        var outcome = await repository.RecordGameResultAsync(gameId, result, finishedAtUtc, cancellationToken);
        await ProvisionIfNextRoundAsync(outcome, cancellationToken);
        return outcome;
    }

    public async Task<MatchResultOutcome> ForceMatchResultAsync(
        Guid tournamentId,
        Guid matchId,
        string result,
        CancellationToken cancellationToken)
    {
        var outcome = await repository.ForceMatchResultAsync(tournamentId, matchId, result, DateTime.UtcNow, cancellationToken);
        await ProvisionIfNextRoundAsync(outcome, cancellationToken);
        return outcome;
    }

    private async Task ProvisionIfNextRoundAsync(MatchResultOutcome outcome, CancellationToken cancellationToken)
    {
        if (outcome.TournamentId is { } tournamentId &&
            outcome.Advance == RoundAdvanceOutcome.NextRoundCreated)
        {
            await TryProvisionAsync(tournamentId, cancellationToken);
        }
    }

    // --------------------------------------------------------------- provisioning

    public async Task<int> RetryFailedProvisionsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var reset = await repository.ResetFailedProvisionsAsync(tournamentId, cancellationToken);
        await TryProvisionAsync(tournamentId, cancellationToken);
        return reset;
    }

    public async Task<ProvisionBatchResult> ProvisionPendingAsync(Guid? tournamentId, CancellationToken cancellationToken)
    {
        var pending = await repository.GetPendingProvisionsAsync(
            tournamentId, DateTime.UtcNow, ProvisionBatchSize, cancellationToken);

        var created = 0;
        var failed = 0;
        var notConfigured = false;

        foreach (var match in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await gameProvisioner.ProvisionAsync(match, cancellationToken);

            if (result.Outcome == ProvisionOutcome.NotConfigured)
            {
                notConfigured = true;
                break;
            }

            if (result.Outcome == ProvisionOutcome.Created)
            {
                if (result.GameId is { } gameId && gameId != Guid.Empty)
                {
                    await repository.AttachGameAsync(match.TournamentId, match.MatchId, gameId, cancellationToken);
                    created++;
                    continue;
                }

                await repository.RecordProvisionFailureAsync(
                    match.MatchId, "Game API returned an invalid response.", permanent: false,
                    DateTime.UtcNow, cancellationToken);
                failed++;
                break;
            }

            var permanent = result.Outcome == ProvisionOutcome.Permanent;
            await repository.RecordProvisionFailureAsync(
                match.MatchId, result.Error ?? "Game provisioning failed.", permanent,
                DateTime.UtcNow, cancellationToken);
            failed++;

            // A transient failure means the Game API is probably down: stop instead of
            // waiting for a timeout on every remaining match. The next cycle continues.
            if (!permanent) break;
        }

        return new ProvisionBatchResult(created, failed, notConfigured);
    }

    private async Task TryProvisionAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        try
        {
            await ProvisionPendingAsync(tournamentId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The committed state is already correct; the background cycle retries.
            logger.LogWarning(ex, "Game provisioning for tournament {TournamentId} failed; it will be retried.", tournamentId);
        }
    }

    public async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        try
        {
            foreach (var id in await repository.GetDueUpcomingIdsAsync(now, 20, cancellationToken))
            {
                try
                {
                    var outcome = await repository.StartAsync(id, now, cancelIfUnderfilled: true, cancellationToken);
                    logger.LogInformation("Auto-start of tournament {TournamentId}: {Outcome}.", id, outcome);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Auto-start of tournament {TournamentId} failed.", id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not look up tournaments that are due to start.");
        }

        try
        {
            foreach (var id in await repository.GetStalledTournamentIdsAsync(20, cancellationToken))
            {
                try
                {
                    var outcome = await repository.AdvanceAsync(id, now, cancellationToken);
                    logger.LogInformation("Advanced stalled tournament {TournamentId}: {Outcome}.", id, outcome);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Advancing tournament {TournamentId} failed.", id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not look up stalled tournaments.");
        }

        try
        {
            var batch = await ProvisionPendingAsync(null, cancellationToken);
            if (batch.NotConfigured)
                logger.LogWarning("Tournament game provisioning is not configured (GameService:BaseUrl / InternalServices:TournamentKey); games will be created once it is.");
            else if (batch.Created > 0 || batch.Failed > 0)
                logger.LogInformation("Game provisioning cycle: {Created} created, {Failed} failed.", batch.Created, batch.Failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Game provisioning cycle failed.");
        }
    }

    private static TournamentDetailsDto Map(ChessTournament t) => new(
        t.Id, t.Name, t.Description, t.Status.ToString(), t.StartsAtUtc, t.CreatedByUserId, t.InitialTimeSeconds, t.IncrementSeconds, t.MaxParticipants,
        t.Participants.Select(p => p.UserId).ToList(),
        t.Matches
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.Id)
            .Select(m => new TournamentMatchDto(m.Id, m.RoundNumber, m.WhitePlayerId, m.BlackPlayerId, m.GameId, m.Status, m.Result, m.WinnerId))
            .ToList(),
        t.TotalRounds, t.CurrentRound, t.StartedAtUtc, t.CompletedAtUtc);
}
