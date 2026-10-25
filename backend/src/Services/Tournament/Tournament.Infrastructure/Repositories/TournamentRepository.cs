using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Tournament.Application.Interfaces;
using Tournament.Application.Pagination;
using Tournament.Domain.Entities;
using Tournament.Domain.Enums;
using Tournament.Domain.Pairing;
using Tournament.Infrastructure.Persistence;

namespace Tournament.Infrastructure.Repositories;

public sealed class TournamentRepository(
    TournamentDbContext dbContext,
    ILogger<TournamentRepository> logger)
    : ITournamentRepository
{
    public async Task<(IReadOnlyList<TournamentListRow> Items, int TotalCount)>
        GetPageAsync(
            int page,
            int pageSize,
            TournamentStatus? status,
            CancellationToken cancellationToken)
    {
        var offset = TournamentPageWindow.GetOffset(page, pageSize);
        var query = dbContext.Tournaments.AsNoTracking();

        query = status.HasValue
            ? query.Where(t => t.Status == status.Value)
            : query.Where(t =>
                t.Status == TournamentStatus.Upcoming ||
                t.Status == TournamentStatus.InProgress);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.StartsAtUtc)
            .ThenBy(t => t.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(t => new TournamentListRow(
                t.Id,
                t.Name,
                t.Description,
                t.Status,
                t.InitialTimeSeconds,
                t.IncrementSeconds,
                t.MaxParticipants,
                t.StartsAtUtc,
                t.Participants.Count))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<JoinAttemptResult?> JoinAsync(
        Guid tournamentId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the tournament row until commit so concurrent joins are
        // serialized and the capacity check below cannot be raced.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM tournaments WHERE "Id" = {tournamentId} FOR UPDATE""",
            cancellationToken);

        var tournament = await dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);

        if (tournament is null)
            return null;

        var participantCount = await dbContext.TournamentParticipants
            .CountAsync(p => p.TournamentId == tournamentId, cancellationToken);

        var alreadyJoined = await dbContext.TournamentParticipants
            .AnyAsync(
                p => p.TournamentId == tournamentId && p.UserId == userId,
                cancellationToken);

        var outcome = tournament.EvaluateJoin(
            participantCount, alreadyJoined, nowUtc);

        if (outcome != JoinTournamentOutcome.Joined)
            return new JoinAttemptResult(outcome, participantCount);

        dbContext.TournamentParticipants.Add(new TournamentParticipant
        {
            TournamentId = tournamentId,
            UserId = userId,
            JoinedAtUtc = nowUtc
        });

        QueueOutbox("tournament:participant-joined", tournamentId, new { userId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new JoinAttemptResult(outcome, participantCount + 1);
    }


    public Task<ChessTournament?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken) =>
        dbContext.Tournaments.Include(t => t.Participants).Include(t => t.Matches)
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);

    public async Task<ChessTournament> CreateAsync(ChessTournament tournament, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Tournaments.Add(tournament);
        QueueOutbox("tournament:created", tournament.Id, new { tournament.Id, tournament.Name, tournament.CreatedByUserId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tournament;
    }


    public Task<bool> CanManageAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Tournaments.AnyAsync(t => t.Id == tournamentId && t.CreatedByUserId == userId, cancellationToken);

    public async Task<bool> CancelAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null || tournament.Status != TournamentStatus.Upcoming || tournament.StartsAtUtc <= DateTime.UtcNow) return false;
        tournament.Status = TournamentStatus.Cancelled;
        QueueOutbox("tournament:cancelled", tournamentId, new { tournamentId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> LeaveAsync(Guid tournamentId, Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null || tournament.Status != TournamentStatus.Upcoming || nowUtc >= tournament.StartsAtUtc) return false;
        var participant = await dbContext.TournamentParticipants.FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId, cancellationToken);
        if (participant is null) return false;
        dbContext.TournamentParticipants.Remove(participant);
        QueueOutbox("tournament:participant-left", tournamentId, new { userId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // ------------------------------------------------------------------ lifecycle
    //
    // Every lifecycle method clears the change tracker first (the DbContext is reused
    // within a request or a background cycle) and then holds the tournament row lock for
    // the whole transaction, so the game-finished consumer, the background worker and the
    // HTTP endpoints cannot advance the same tournament twice.

    public async Task<StartTournamentOutcome> StartAsync(
        Guid tournamentId,
        DateTime nowUtc,
        bool cancelIfUnderfilled,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null) return StartTournamentOutcome.NotFound;
        if (tournament.Status != TournamentStatus.Upcoming) return StartTournamentOutcome.InvalidState;
        if (nowUtc < tournament.StartsAtUtc) return StartTournamentOutcome.TooEarly;

        var participantIds = await dbContext.TournamentParticipants
            .Where(p => p.TournamentId == tournamentId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        if (participantIds.Count < TournamentFormat.MinPlayers)
        {
            if (!cancelIfUnderfilled) return StartTournamentOutcome.NotEnoughPlayers;

            tournament.Status = TournamentStatus.Cancelled;
            QueueOutbox("tournament:cancelled", tournamentId, new { tournamentId, reason = "NotEnoughPlayers" });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return StartTournamentOutcome.Cancelled;
        }

        tournament.Status = TournamentStatus.InProgress;
        tournament.StartedAtUtc = nowUtc;
        tournament.TotalRounds = TournamentFormat.RoundsFor(participantIds.Count);

        var snapshots = SwissPairer.BuildSnapshots(participantIds, Array.Empty<TournamentMatch>());
        AddRound(tournament, 1, SwissPairer.Pair(snapshots), nowUtc);
        QueueOutbox("tournament:started", tournamentId, new { tournamentId, currentRound = 1, totalRounds = tournament.TotalRounds });
        QueueOutbox("tournament:pairings-updated", tournamentId, new { tournamentId, currentRound = 1 });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StartTournamentOutcome.Started;
    }

    public async Task<RoundAdvanceOutcome> AdvanceAsync(
        Guid tournamentId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null) return RoundAdvanceOutcome.NotFound;

        var outcome = await AdvanceLockedAsync(tournament, nowUtc, cancellationToken);
        if (outcome == RoundAdvanceOutcome.NextRoundCreated)
            QueueOutbox("tournament:pairings-updated", tournamentId, new { tournamentId, currentRound = tournament.CurrentRound });
        else if (outcome == RoundAdvanceOutcome.Completed)
            QueueOutbox("tournament:completed", tournamentId, new { tournamentId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    public async Task<MatchResultOutcome> RecordGameResultAsync(
        Guid matchId,
        string result,
        DateTime finishedAtUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        // The match id doubles as the game id, so a finished game that is not a
        // tournament game simply has no matching row.
        var tournamentId = await dbContext.TournamentMatches
            .AsNoTracking()
            .Where(m => m.Id == matchId)
            .Select(m => (Guid?)m.TournamentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (tournamentId is null)
            return new MatchResultOutcome(MatchResultStatus.NotATournamentGame);

        if (!MatchResults.IsPlayableResult(result))
            return new MatchResultOutcome(MatchResultStatus.InvalidResult, tournamentId);

        return await SettleMatchAsync(tournamentId.Value, matchId, result, finishedAtUtc, requireInProgress: false, cancellationToken);
    }

    public async Task<MatchResultOutcome> ForceMatchResultAsync(
        Guid tournamentId,
        Guid matchId,
        string result,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        if (!MatchResults.IsPlayableResult(result))
            return new MatchResultOutcome(MatchResultStatus.InvalidResult, tournamentId);

        return await SettleMatchAsync(tournamentId, matchId, result, nowUtc, requireInProgress: true, cancellationToken);
    }

    private async Task<MatchResultOutcome> SettleMatchAsync(
        Guid tournamentId,
        Guid matchId,
        string result,
        DateTime atUtc,
        bool requireInProgress,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null)
            return new MatchResultOutcome(MatchResultStatus.MatchNotFound);

        if (requireInProgress && tournament.Status != TournamentStatus.InProgress)
            return new MatchResultOutcome(MatchResultStatus.InvalidState, tournamentId);

        var match = await dbContext.TournamentMatches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId, cancellationToken);
        if (match is null)
            return new MatchResultOutcome(MatchResultStatus.MatchNotFound, tournamentId);

        if (match.IsSettled)
            return new MatchResultOutcome(MatchResultStatus.AlreadyRecorded, tournamentId);

        if (!match.BlackPlayerId.HasValue)
            return new MatchResultOutcome(MatchResultStatus.InvalidState, tournamentId);

        match.Settle(result, atUtc);
        match.GameId ??= match.Id;

        // The result is stored even if pairing the next round fails; the background worker
        // retries the advance (GetStalledTournamentIdsAsync) instead of losing the result.
        RoundAdvanceOutcome? advance = null;
        if (tournament.Status == TournamentStatus.InProgress)
        {
            try
            {
                advance = await AdvanceLockedAsync(tournament, atUtc, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Result of match {MatchId} stored, but advancing tournament {TournamentId} failed; it will be retried.", matchId, tournamentId);
            }
        }

        QueueOutbox("tournament:match-result", tournamentId, new { tournamentId, matchId, result });
        if (advance == RoundAdvanceOutcome.NextRoundCreated)
            QueueOutbox("tournament:pairings-updated", tournamentId, new { tournamentId });
        else if (advance == RoundAdvanceOutcome.Completed)
            QueueOutbox("tournament:completed", tournamentId, new { tournamentId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MatchResultOutcome(MatchResultStatus.Recorded, tournamentId, advance);
    }

    public async Task<CompleteTournamentOutcome> CompleteAsync(
        Guid tournamentId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTournamentAsync(tournamentId, cancellationToken);

        var tournament = await dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
        if (tournament is null) return CompleteTournamentOutcome.NotFound;
        if (tournament.Status != TournamentStatus.InProgress) return CompleteTournamentOutcome.InvalidState;
        if (tournament.TotalRounds < 1 || tournament.CurrentRound < tournament.TotalRounds ||
            await dbContext.TournamentMatches.AnyAsync(
                match => match.TournamentId == tournamentId &&
                         match.Status != MatchStatuses.Finished &&
                         match.Status != MatchStatuses.Bye,
                cancellationToken))
            return CompleteTournamentOutcome.NotReady;

        tournament.Status = TournamentStatus.Completed;
        tournament.CompletedAtUtc = nowUtc;
        QueueOutbox("tournament:completed", tournamentId, new { tournamentId });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CompleteTournamentOutcome.Completed;
    }

    // Caller holds the tournament row lock and a transaction; the caller saves.
    private async Task<RoundAdvanceOutcome> AdvanceLockedAsync(
        ChessTournament tournament,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (tournament.Status != TournamentStatus.InProgress || tournament.CurrentRound < 1)
            return RoundAdvanceOutcome.InvalidState;

        // Tracked query: rows changed earlier in this transaction keep their new values.
        var matches = await dbContext.TournamentMatches
            .Where(m => m.TournamentId == tournament.Id)
            .ToListAsync(cancellationToken);

        var roundOpen = matches.Any(m => m.RoundNumber == tournament.CurrentRound && !m.IsSettled);
        if (roundOpen) return RoundAdvanceOutcome.RoundInProgress;

        if (tournament.CurrentRound >= tournament.TotalRounds)
        {
            tournament.Status = TournamentStatus.Completed;
            tournament.CompletedAtUtc = nowUtc;
            return RoundAdvanceOutcome.Completed;
        }

        var participantIds = await dbContext.TournamentParticipants
            .Where(p => p.TournamentId == tournament.Id)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var snapshots = SwissPairer.BuildSnapshots(participantIds, matches);
        AddRound(tournament, tournament.CurrentRound + 1, SwissPairer.Pair(snapshots), nowUtc);
        return RoundAdvanceOutcome.NextRoundCreated;
    }

    private void AddRound(
        ChessTournament tournament,
        int roundNumber,
        IReadOnlyList<PairingDraft> drafts,
        DateTime nowUtc)
    {
        tournament.CurrentRound = roundNumber;

        foreach (var draft in drafts)
        {
            var isBye = !draft.BlackId.HasValue;
            dbContext.TournamentMatches.Add(new TournamentMatch
            {
                Id = Guid.NewGuid(),
                TournamentId = tournament.Id,
                RoundNumber = roundNumber,
                WhitePlayerId = draft.WhiteId,
                BlackPlayerId = draft.BlackId,
                Status = isBye ? MatchStatuses.Bye : MatchStatuses.Scheduled,
                Result = isBye ? MatchResults.Bye : null,
                FinishedAtUtc = isBye ? nowUtc : null
            });
        }
    }

    private Task LockTournamentAsync(Guid tournamentId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM tournaments WHERE "Id" = {tournamentId} FOR UPDATE""",
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetDueUpcomingIdsAsync(
        DateTime nowUtc,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.Tournaments
            .AsNoTracking()
            .Where(t => t.Status == TournamentStatus.Upcoming && t.StartsAtUtc <= nowUtc)
            .OrderBy(t => t.StartsAtUtc)
            .Select(t => t.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetStalledTournamentIdsAsync(
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.Tournaments
            .AsNoTracking()
            .Where(t => t.Status == TournamentStatus.InProgress
                        && t.CurrentRound > 0
                        && !t.Matches.Any(m => m.RoundNumber == t.CurrentRound
                                               && m.Status != MatchStatuses.Finished
                                               && m.Status != MatchStatuses.Bye))
            .Select(t => t.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------ provisioning

    public async Task<IReadOnlyList<PendingProvision>> GetPendingProvisionsAsync(
        Guid? tournamentId,
        DateTime nowUtc,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TournamentMatches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatuses.Scheduled
                        && m.BlackPlayerId != null
                        && m.GameId == null
                        && m.Tournament.Status == TournamentStatus.InProgress
                        && (m.NextProvisionAtUtc == null || m.NextProvisionAtUtc <= nowUtc));

        if (tournamentId.HasValue)
            query = query.Where(m => m.TournamentId == tournamentId.Value);

        return await query
            .OrderBy(m => m.ProvisionAttempts)
            .ThenBy(m => m.RoundNumber)
            .Take(take)
            .Select(m => new PendingProvision(
                m.Id,
                m.TournamentId,
                m.WhitePlayerId,
                m.BlackPlayerId ?? Guid.Empty,
                m.Tournament.InitialTimeSeconds,
                m.Tournament.IncrementSeconds))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AttachGameAsync(
        Guid tournamentId,
        Guid matchId,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var match = await dbContext.TournamentMatches
            .FirstOrDefaultAsync(m => m.TournamentId == tournamentId && m.Id == matchId, cancellationToken);
        if (match is null) return false;
        if (match.GameId.HasValue && match.GameId.Value != gameId)
            throw new InvalidOperationException("A different game is already attached to this match.");

        // Already settled (result forced before the game was attached): nothing to do.
        if (match.IsSettled) return true;

        match.GameId = gameId;
        if (match.Status is MatchStatuses.Scheduled or MatchStatuses.ProvisionFailed)
            match.Status = MatchStatuses.InProgress;
        match.NextProvisionAtUtc = null;
        match.LastProvisionError = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordProvisionFailureAsync(
        Guid matchId,
        string error,
        bool permanent,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var match = await dbContext.TournamentMatches
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);
        if (match is null || match.IsSettled || match.GameId.HasValue) return;

        match.ProvisionAttempts++;
        match.LastProvisionError = error.Length > 200 ? error[..200] : error;

        if (permanent)
        {
            match.Status = MatchStatuses.ProvisionFailed;
            match.NextProvisionAtUtc = null;
        }
        else
        {
            // 5s, 10s, 20s ... capped at 5 minutes.
            var seconds = Math.Min(300d, 5d * Math.Pow(2, Math.Min(match.ProvisionAttempts, 10) - 1));
            match.NextProvisionAtUtc = nowUtc.AddSeconds(seconds);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResetFailedProvisionsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var failed = await dbContext.TournamentMatches
            .Where(m => m.TournamentId == tournamentId && m.Status == MatchStatuses.ProvisionFailed)
            .ToListAsync(cancellationToken);

        foreach (var match in failed)
        {
            match.Status = MatchStatuses.Scheduled;
            match.ProvisionAttempts = 0;
            match.NextProvisionAtUtc = null;
            match.LastProvisionError = null;
        }

        if (failed.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return failed.Count;
    }

    private void QueueOutbox(string eventType, Guid tournamentId, object? data)
    {
        dbContext.OutboxMessages.Add(new TournamentOutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            TournamentId = tournamentId,
            DataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        });
    }

}
