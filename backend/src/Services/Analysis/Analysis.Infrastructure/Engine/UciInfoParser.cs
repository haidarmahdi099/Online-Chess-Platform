using System.Globalization;
using Analysis.Domain.Evaluation;

namespace Analysis.Infrastructure.Engine;

internal sealed record UciInfo(
    ScoreType ScoreType,
    int Score,
    int Depth,
    IReadOnlyList<string> PrincipalVariation);

internal static class UciInfoParser
{
    // Parses a line such as:
    // info depth 12 seldepth 18 multipv 1 score cp 34 nodes 1000 pv e2e4 e7e5
    // Returns null for lines that carry no usable primary-line score.
    public static UciInfo? TryParse(string line)
    {
        if (!line.StartsWith("info ", StringComparison.Ordinal))
            return null;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        ScoreType? scoreType = null;
        var score = 0;
        var depth = 0;
        var pv = new List<string>();

        for (var i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "depth" when i + 1 < tokens.Length:
                    int.TryParse(tokens[i + 1], NumberStyles.None,
                        CultureInfo.InvariantCulture, out depth);
                    break;

                case "multipv" when i + 1 < tokens.Length:
                    if (tokens[i + 1] != "1")
                        return null;
                    break;

                case "score" when i + 2 < tokens.Length:
                    if (!int.TryParse(tokens[i + 2], NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture, out score))
                        return null;

                    scoreType = tokens[i + 1] switch
                    {
                        "cp" => ScoreType.Centipawns,
                        "mate" => ScoreType.Mate,
                        _ => null
                    };

                    // Bound scores are partial aspiration-window results.
                    if (i + 3 < tokens.Length &&
                        tokens[i + 3] is "lowerbound" or "upperbound")
                        return null;
                    break;

                case "pv":
                    pv.AddRange(tokens.Skip(i + 1));
                    i = tokens.Length;
                    break;
            }
        }

        return scoreType is null
            ? null
            : new UciInfo(scoreType.Value, score, depth, pv);
    }
}
