using System.Globalization;
using System.Text;
using Game.Domain.Enums;

namespace Game.Application.Games;

public static class PgnWriter
{
    private const int LineWidth = 80;

    public static string Write(
        Game.Domain.Entities.Game game,
        IReadOnlyList<MoveHistoryItem> moves)
    {
        var result = ResultToken(game);
        var created = game.CreatedAt;

        var sb = new StringBuilder();

        Tag(sb, "Event", game.Rated ? "Rated game" : "Casual game");
        Tag(sb, "Site", "Echelon Chess");
        Tag(sb, "Date", created.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture));
        Tag(sb, "Round", "-");
        // The Game service only knows user ids; clients can substitute usernames if they wish.
        Tag(sb, "White", game.WhitePlayerId.ToString());
        Tag(sb, "Black", game.BlackPlayerId.ToString());
        Tag(sb, "Result", result);
        Tag(sb, "UTCDate", created.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture));
        Tag(sb, "UTCTime", created.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        Tag(sb, "TimeControl", $"{game.InitialTimeSeconds}+{game.IncrementSeconds}");

        if (game.EndReason is not null)
        {
            Tag(sb, "Termination", game.EndReason.Value.ToString());
        }

        sb.Append('\n');

        var tokens = new List<string>(moves.Count + moves.Count / 2 + 1);
        foreach (var move in moves)
        {
            if (move.Color == "white")
            {
                tokens.Add($"{move.MoveNumber}.");
            }
            else if (tokens.Count == 0)
            {
                // Game that starts from Black's move: PGN spells this "1..."
                tokens.Add($"{move.MoveNumber}...");
            }

            tokens.Add(move.San);
        }

        tokens.Add(result);

        var line = new StringBuilder();
        foreach (var token in tokens)
        {
            if (line.Length > 0 && line.Length + 1 + token.Length > LineWidth)
            {
                sb.Append(line).Append('\n');
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');
            line.Append(token);
        }

        sb.Append(line).Append('\n');

        return sb.ToString();
    }

    private static string ResultToken(Game.Domain.Entities.Game game)
    {
        if (game.Status != GameStatus.Finished)
        {
            return "*";
        }

        if (game.WinnerId == game.WhitePlayerId) return "1-0";
        if (game.WinnerId == game.BlackPlayerId) return "0-1";
        return "1/2-1/2";
    }

    private static void Tag(StringBuilder sb, string name, string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.Append('[').Append(name).Append(" \"").Append(escaped).Append("\"]\n");
    }
}
