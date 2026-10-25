namespace Analysis.Domain.Chess;

// A structurally validated FEN string. Because only a strict character set
// can get through, the value is safe to forward to a chess engine as-is.
public sealed class FenPosition
{
    private const string PieceChars = "pnbrqkPNBRQK";

    public string Fen { get; }

    public bool IsWhiteToMove { get; }

    private FenPosition(string fen, bool isWhiteToMove)
    {
        Fen = fen;
        IsWhiteToMove = isWhiteToMove;
    }

    public static bool TryCreate(
        string? fen,
        out FenPosition? position,
        out string? error)
    {
        position = null;
        error = Validate(fen, out var normalized, out var whiteToMove);

        if (error is not null)
            return false;

        position = new FenPosition(normalized!, whiteToMove);
        return true;
    }

    private static string? Validate(
        string? fen,
        out string? normalized,
        out bool whiteToMove)
    {
        normalized = null;
        whiteToMove = true;

        if (string.IsNullOrWhiteSpace(fen))
            return "FEN is required.";

        var value = fen.Trim();

        if (value.Length > 100)
            return "FEN is too long.";

        var fields = value.Split(' ');

        if (fields.Length != 6 || fields.Any(f => f.Length == 0))
            return "FEN must contain six space-separated fields.";

        var ranks = fields[0].Split('/');

        if (ranks.Length != 8)
            return "FEN piece placement must contain eight ranks.";

        var whiteKings = 0;
        var blackKings = 0;
        var whitePieces = 0;
        var blackPieces = 0;
        var whitePawns = 0;
        var blackPawns = 0;

        for (var rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
        {
            var squares = 0;
            var previousWasDigit = false;

            foreach (var c in ranks[rankIndex])
            {
                if (c is >= '1' and <= '8')
                {
                    if (previousWasDigit)
                        return "FEN piece placement is invalid.";

                    squares += c - '0';
                    previousWasDigit = true;
                    continue;
                }

                previousWasDigit = false;

                if (PieceChars.IndexOf(c) < 0)
                    return "FEN piece placement contains an invalid character.";

                squares++;

                var isWhite = char.IsUpper(c);

                if (isWhite) whitePieces++; else blackPieces++;

                switch (char.ToLowerInvariant(c))
                {
                    case 'k':
                        if (isWhite) whiteKings++; else blackKings++;
                        break;
                    case 'p':
                        // rankIndex 0 is rank 8 and rankIndex 7 is rank 1.
                        if (rankIndex is 0 or 7)
                            return "Pawns cannot stand on the first or last rank.";
                        if (isWhite) whitePawns++; else blackPawns++;
                        break;
                }
            }

            if (squares != 8)
                return "Each FEN rank must describe exactly eight squares.";
        }

        if (whiteKings != 1 || blackKings != 1)
            return "Each side must have exactly one king.";

        if (whitePieces > 16 || blackPieces > 16 ||
            whitePawns > 8 || blackPawns > 8)
            return "FEN contains too many pieces.";

        if (fields[1] is not ("w" or "b"))
            return "FEN side to move must be 'w' or 'b'.";

        if (fields[2] != "-" &&
            (fields[2].Length > 4 ||
             fields[2].Any(c => "KQkq".IndexOf(c) < 0) ||
             fields[2].Distinct().Count() != fields[2].Length))
            return "FEN castling rights are invalid.";

        if (fields[3] != "-" &&
            !(fields[3].Length == 2 &&
              fields[3][0] is >= 'a' and <= 'h' &&
              fields[3][1] is '3' or '6'))
            return "FEN en passant square is invalid.";

        if (fields[3] != "-" &&
            ((fields[1] == "w" && fields[3][1] != '6') ||
             (fields[1] == "b" && fields[3][1] != '3')))
            return "FEN en passant square is inconsistent with the side to move.";

        if (!int.TryParse(fields[4], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return "FEN halfmove clock is invalid.";

        if (!int.TryParse(fields[5], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var fullmove) ||
            fullmove < 1)
            return "FEN fullmove number is invalid.";

        normalized = string.Join(' ', fields);
        whiteToMove = fields[1] == "w";
        return null;
    }
}
