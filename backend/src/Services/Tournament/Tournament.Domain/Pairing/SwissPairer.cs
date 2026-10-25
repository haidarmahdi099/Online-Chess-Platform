using Tournament.Domain.Entities;

namespace Tournament.Domain.Pairing;

public sealed record PlayerSnapshot(
    Guid UserId,
    decimal Points,
    decimal Buchholz,
    int WhiteGames,
    int BlackGames,
    bool? LastWasWhite,
    bool HadBye,
    IReadOnlySet<Guid> Opponents);

/// <summary>BlackId == null means WhiteId receives the bye.</summary>
public sealed record PairingDraft(Guid WhiteId, Guid? BlackId);

public static class SwissPairer
{
    private const int SearchBudgetPerAttempt = 50_000;

    public static IReadOnlyList<PlayerSnapshot> BuildSnapshots(
        IReadOnlyCollection<Guid> participantIds,
        IReadOnlyCollection<TournamentMatch> matches)
    {
        var standings = StandingsCalculator.Compute(participantIds, matches)
            .ToDictionary(r => r.UserId);

        var realMatches = matches
            .Where(m => m.BlackPlayerId.HasValue && m.Status != MatchStatuses.Bye)
            .OrderBy(m => m.RoundNumber)
            .ToList();

        return participantIds.Distinct().Select(id =>
        {
            var opponents = new HashSet<Guid>();
            var whites = 0;
            var blacks = 0;
            bool? last = null;

            foreach (var m in realMatches)
            {
                if (m.WhitePlayerId == id)
                {
                    whites++;
                    opponents.Add(m.BlackPlayerId!.Value);
                    last = true;
                }
                else if (m.BlackPlayerId == id)
                {
                    blacks++;
                    opponents.Add(m.WhitePlayerId);
                    last = false;
                }
            }

            var hadBye = matches.Any(m => m.WhitePlayerId == id &&
                                          (m.Status == MatchStatuses.Bye || !m.BlackPlayerId.HasValue));
            var row = standings[id];

            return new PlayerSnapshot(id, row.Points, row.Buchholz, whites, blacks, last, hadBye, opponents);
        }).ToList();
    }

    /// <summary>
    /// Pairs one Swiss round. Round 1 (nobody has played) is random. Later rounds sort by
    /// points then Buchholz and pair the closest players who have not met yet; with an odd
    /// field the bye goes to the lowest-ranked player who has not had one. If no
    /// rematch-free pairing exists (or the search budget runs out) rematches are allowed
    /// as a last resort so a round can always be produced.
    /// </summary>
    public static IReadOnlyList<PairingDraft> Pair(
        IReadOnlyList<PlayerSnapshot> players,
        Random? random = null)
    {
        if (players.Count < TournamentFormat.MinPlayers)
            throw new InvalidOperationException("At least two participants are required.");

        var rng = random ?? Random.Shared;
        var firstRound = players.All(p => p.Opponents.Count == 0 && !p.HadBye);

        var ordered = firstRound
            ? players.OrderBy(_ => rng.Next()).ToList()
            : players
                .OrderByDescending(p => p.Points)
                .ThenByDescending(p => p.Buchholz)
                .ThenBy(p => p.UserId)
                .ToList();

        // Bye candidates, lowest ranked first. Even field: a single "no bye" attempt.
        var byeCandidates = new List<PlayerSnapshot?>();
        if (ordered.Count % 2 == 1)
        {
            var fresh = Enumerable.Reverse(ordered).Where(p => !p.HadBye).ToList();
            byeCandidates.AddRange(fresh);
            if (byeCandidates.Count == 0)
                byeCandidates.AddRange(Enumerable.Reverse(ordered));
        }
        else
        {
            byeCandidates.Add(null);
        }

        foreach (var avoidRematch in new[] { true, false })
        {
            foreach (var bye in byeCandidates)
            {
                var pool = bye is null ? ordered : ordered.Where(p => p.UserId != bye.UserId).ToList();
                var used = new bool[pool.Count];
                var accepted = new List<(PlayerSnapshot A, PlayerSnapshot B)>();
                var budget = avoidRematch ? SearchBudgetPerAttempt : int.MaxValue;

                if (!Match(pool, used, 0, avoidRematch, accepted, ref budget))
                    continue;

                var drafts = accepted.Select(pair => AssignColors(pair.A, pair.B)).ToList();
                if (bye is not null)
                    drafts.Add(new PairingDraft(bye.UserId, null));
                return drafts;
            }
        }

        throw new InvalidOperationException("Could not produce a pairing for this round.");
    }

    private static bool Match(
        List<PlayerSnapshot> pool,
        bool[] used,
        int start,
        bool avoidRematch,
        List<(PlayerSnapshot A, PlayerSnapshot B)> accepted,
        ref int budget)
    {
        var i = start;
        while (i < pool.Count && used[i]) i++;
        if (i == pool.Count) return true;

        used[i] = true;
        for (var j = i + 1; j < pool.Count; j++)
        {
            if (used[j]) continue;
            if (avoidRematch && pool[i].Opponents.Contains(pool[j].UserId)) continue;
            if (--budget < 0)
            {
                used[i] = false;
                return false;
            }

            used[j] = true;
            accepted.Add((pool[i], pool[j]));
            if (Match(pool, used, i + 1, avoidRematch, accepted, ref budget))
                return true;

            accepted.RemoveAt(accepted.Count - 1);
            used[j] = false;
        }

        used[i] = false;
        return false;
    }

    // A is the higher-ranked player. Colour balance first, then alternate from the last
    // game, otherwise the higher-ranked player keeps white.
    private static PairingDraft AssignColors(PlayerSnapshot a, PlayerSnapshot b)
    {
        var balanceA = a.WhiteGames - a.BlackGames;
        var balanceB = b.WhiteGames - b.BlackGames;

        if (balanceA != balanceB)
            return balanceA < balanceB
                ? new PairingDraft(a.UserId, b.UserId)
                : new PairingDraft(b.UserId, a.UserId);

        var aLastWhite = a.LastWasWhite == true;
        var bLastWhite = b.LastWasWhite == true;
        if (aLastWhite != bLastWhite)
            return aLastWhite
                ? new PairingDraft(b.UserId, a.UserId)
                : new PairingDraft(a.UserId, b.UserId);

        return new PairingDraft(a.UserId, b.UserId);
    }
}
