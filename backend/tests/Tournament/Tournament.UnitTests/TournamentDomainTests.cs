using Tournament.Domain.Entities;
using Tournament.Domain.Enums;
using Tournament.Domain.Pairing;

namespace Tournament.UnitTests;

public sealed class TournamentDomainTests
{
    [Fact]
    public void EvaluateJoin_enforces_registration_state_capacity_and_duplicate_seats()
    {
        var now = DateTime.UtcNow;
        var tournament = NewTournament(now.AddHours(1), maxParticipants: 2);

        Assert.Equal(JoinTournamentOutcome.Joined, tournament.EvaluateJoin(0, false, now));
        Assert.Equal(JoinTournamentOutcome.AlreadyJoined, tournament.EvaluateJoin(1, true, now));
        Assert.Equal(JoinTournamentOutcome.TournamentFull, tournament.EvaluateJoin(2, false, now));

        tournament.Status = TournamentStatus.InProgress;
        Assert.Equal(JoinTournamentOutcome.RegistrationClosed, tournament.EvaluateJoin(1, false, now));
    }

    [Fact]
    public void EvaluateJoin_closes_registration_at_start_time()
    {
        var startsAt = DateTime.UtcNow;
        var tournament = NewTournament(startsAt);

        Assert.Equal(JoinTournamentOutcome.RegistrationClosed,
            tournament.EvaluateJoin(0, false, startsAt));
    }

    [Fact]
    public void TournamentMatch_settle_records_winner_and_finishing_time()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var match = new TournamentMatch { WhitePlayerId = white, BlackPlayerId = black };
        var finishedAt = DateTime.UtcNow;

        match.Settle(MatchResults.BlackWin, finishedAt);

        Assert.True(match.IsSettled);
        Assert.Equal(MatchStatuses.Finished, match.Status);
        Assert.Equal(MatchResults.BlackWin, match.Result);
        Assert.Equal(black, match.WinnerId);
        Assert.Equal(finishedAt, match.FinishedAtUtc);
    }

    [Fact]
    public void TournamentMatch_rejects_non_playable_result()
    {
        var match = new TournamentMatch { WhitePlayerId = Guid.NewGuid(), BlackPlayerId = Guid.NewGuid() };

        Assert.Throws<ArgumentException>(() => match.Settle("Bye", DateTime.UtcNow));
    }

    [Fact]
    public void Standings_count_only_settled_matches_and_award_correct_points()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var idle = Guid.NewGuid();
        var matches = new[]
        {
            new TournamentMatch { WhitePlayerId = white, BlackPlayerId = black, Status = MatchStatuses.Finished, Result = MatchResults.WhiteWin },
            new TournamentMatch { WhitePlayerId = white, BlackPlayerId = idle, Status = MatchStatuses.Scheduled }
        };

        var rows = StandingsCalculator.Compute(new[] { white, black, idle }, matches).ToDictionary(x => x.UserId);

        Assert.Equal(1m, rows[white].Points);
        Assert.Equal(1, rows[white].Wins);
        Assert.Equal(1, rows[white].GamesPlayed);
        Assert.Equal(0m, rows[black].Points);
        Assert.Equal(1, rows[black].Losses);
        Assert.Equal(0, rows[idle].GamesPlayed);
    }

    [Fact]
    public void Standings_award_one_point_for_bye()
    {
        var player = Guid.NewGuid();
        var bye = new TournamentMatch { WhitePlayerId = player, BlackPlayerId = null, Status = MatchStatuses.Bye, Result = MatchResults.Bye };

        var row = Assert.Single(StandingsCalculator.Compute(new[] { player }, new[] { bye }));

        Assert.Equal(1m, row.Points);
        Assert.Equal(1, row.Byes);
        Assert.Equal(0, row.GamesPlayed);
    }

    [Fact]
    public void SwissPairer_pairs_each_player_once_and_gives_single_bye_for_odd_field()
    {
        var players = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var snapshots = SwissPairer.BuildSnapshots(players, Array.Empty<TournamentMatch>());

        var pairings = SwissPairer.Pair(snapshots, new Random(123));

        Assert.Equal(3, pairings.Count);
        Assert.Equal(5, pairings.SelectMany(p => p.BlackId.HasValue ? new[] { p.WhiteId, p.BlackId.Value } : new[] { p.WhiteId }).Distinct().Count());
        Assert.Single(pairings, p => p.BlackId is null);
    }

    [Fact]
    public void SwissPairer_avoids_repeat_opponents_when_alternative_pairing_exists()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var players = new[]
        {
            new PlayerSnapshot(a, 1m, 0m, 1, 0, true, false, new HashSet<Guid> { b }),
            new PlayerSnapshot(b, 0m, 0m, 0, 1, false, false, new HashSet<Guid> { a }),
            new PlayerSnapshot(c, 1m, 0m, 1, 0, true, false, new HashSet<Guid>()),
            new PlayerSnapshot(d, 0m, 0m, 0, 1, false, false, new HashSet<Guid>())
        };

        var pairings = SwissPairer.Pair(players, new Random(1));

        Assert.Equal(2, pairings.Count);
        Assert.DoesNotContain(pairings, p => p.BlackId is { } black &&
            ((p.WhiteId == a && black == b) || (p.WhiteId == b && black == a)));
    }

    [Fact]
    public void TournamentFormat_calculates_reasonable_swiss_round_count()
    {
        Assert.Equal(1, TournamentFormat.RoundsFor(2));
        Assert.Equal(2, TournamentFormat.RoundsFor(4));
        Assert.Equal(3, TournamentFormat.RoundsFor(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => TournamentFormat.RoundsFor(1));
    }

    private static ChessTournament NewTournament(DateTime startsAt, int maxParticipants = 16) => new()
    {
        Name = "Test tournament",
        CreatedByUserId = Guid.NewGuid(),
        StartsAtUtc = startsAt,
        MaxParticipants = maxParticipants
    };
}
