using Tournament.Domain.Entities;

namespace Tournament.Domain.Pairing;

public sealed record StandingRow(
    Guid UserId,
    decimal Points,
    int Wins,
    int Draws,
    int Losses,
    int Byes,
    int GamesPlayed,
    decimal Buchholz);

public static class StandingsCalculator
{
    /// <summary>
    /// Win = 1, draw = 0.5, loss = 0, bye = 1. Only settled matches count.
    /// Tiebreaks: Buchholz (sum of opponents' points), then wins, then user id
    /// (only to keep the order stable).
    /// </summary>
    public static IReadOnlyList<StandingRow> Compute(
        IEnumerable<Guid> participantIds,
        IEnumerable<TournamentMatch> matches)
    {
        var points = new Dictionary<Guid, decimal>();
        var wins = new Dictionary<Guid, int>();
        var draws = new Dictionary<Guid, int>();
        var losses = new Dictionary<Guid, int>();
        var byes = new Dictionary<Guid, int>();
        var played = new Dictionary<Guid, int>();
        var opponents = new Dictionary<Guid, List<Guid>>();

        foreach (var id in participantIds.Distinct())
        {
            points[id] = 0m;
            wins[id] = 0;
            draws[id] = 0;
            losses[id] = 0;
            byes[id] = 0;
            played[id] = 0;
            opponents[id] = new List<Guid>();
        }

        var settled = matches.Where(m => m.IsSettled).ToList();

        foreach (var m in settled)
        {
            EnsurePlayer(m.WhitePlayerId);

            if (m.Status == MatchStatuses.Bye || !m.BlackPlayerId.HasValue)
            {
                points[m.WhitePlayerId] += 1m;
                byes[m.WhitePlayerId]++;
                continue;
            }

            var white = m.WhitePlayerId;
            var black = m.BlackPlayerId.Value;
            EnsurePlayer(black);

            played[white]++;
            played[black]++;
            opponents[white].Add(black);
            opponents[black].Add(white);

            switch (m.Result)
            {
                case MatchResults.WhiteWin:
                    points[white] += 1m; wins[white]++; losses[black]++;
                    break;
                case MatchResults.BlackWin:
                    points[black] += 1m; wins[black]++; losses[white]++;
                    break;
                default:
                    points[white] += 0.5m; points[black] += 0.5m;
                    draws[white]++; draws[black]++;
                    break;
            }
        }

        return points.Keys
            .Select(id => new StandingRow(
                id,
                points[id],
                wins[id],
                draws[id],
                losses[id],
                byes[id],
                played[id],
                opponents[id].Sum(o => points[o])))
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Buchholz)
            .ThenByDescending(r => r.Wins)
            .ThenBy(r => r.UserId)
            .ToList();

        void EnsurePlayer(Guid id)
        {
            if (points.ContainsKey(id)) return;
            points[id] = 0m; wins[id] = 0; draws[id] = 0; losses[id] = 0;
            byes[id] = 0; played[id] = 0; opponents[id] = new List<Guid>();
        }
    }
}
