namespace Game.Domain.Chess;

/// <summary>
/// Builds Standard Algebraic Notation (SAN) for a move that was already validated and persisted.
/// Legality (needed for disambiguation and the check/mate suffix) is delegated to <see cref="IChessRules"/>,
/// so this class does not duplicate any move-generation logic.
/// </summary>
public static class SanNotation
{
    public static string Build(
        IChessRules rules,
        string fenBefore,
        string from,
        string to,
        string? promotion)
    {
        var board = ChessPositionAnalyzer.ParseBoard(fenBefore);

        var (fromRow, fromFile) = ToIndex(from);
        var (toRow, toFile) = ToIndex(to);

        var piece = board[fromRow, fromFile];
        var type = char.ToUpperInvariant(piece);
        var target = board[toRow, toFile];

        string san;

        if (type == 'K' && Math.Abs(toFile - fromFile) == 2)
        {
            san = toFile > fromFile ? "O-O" : "O-O-O";
        }
        else if (type == 'P')
        {
            // A pawn changing file is always a capture (this includes en passant, where the
            // destination square is empty).
            var isCapture = fromFile != toFile;

            san = isCapture
                ? $"{FileLetter(fromFile)}x{to}"
                : to;

            if (!string.IsNullOrWhiteSpace(promotion))
            {
                san += "=" + char.ToUpperInvariant(promotion.Trim()[0]);
            }
        }
        else
        {
            var isCapture = target != '\0';

            san = $"{type}{Disambiguation(rules, fenBefore, board, piece, fromRow, fromFile, to)}"
                  + (isCapture ? "x" : "")
                  + to;
        }

        return san + CheckSuffix(rules, fenBefore, from, to, promotion);
    }

    private static string Disambiguation(
        IChessRules rules,
        string fenBefore,
        char[,] board,
        char piece,
        int fromRow,
        int fromFile,
        string to)
    {
        var position = ChessPosition.FromFen(fenBefore);

        var sameFile = false;
        var sameRank = false;
        var ambiguous = false;

        for (var row = 0; row < 8; row++)
        {
            for (var file = 0; file < 8; file++)
            {
                if (board[row, file] != piece || (row == fromRow && file == fromFile))
                {
                    continue;
                }

                if (!CanMove(rules, position, FromIndex(row, file), to))
                {
                    continue;
                }

                ambiguous = true;
                if (file == fromFile) sameFile = true;
                if (row == fromRow) sameRank = true;
            }
        }

        if (!ambiguous) return "";
        if (!sameFile) return FileLetter(fromFile).ToString();
        if (!sameRank) return (8 - fromRow).ToString();
        return FileLetter(fromFile).ToString() + (8 - fromRow);
    }

    private static bool CanMove(
        IChessRules rules,
        ChessPosition position,
        string from,
        string to)
    {
        try
        {
            return rules.ValidateAndApply(position, new ChessMove(from, to, null)).IsLegal;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string CheckSuffix(
        IChessRules rules,
        string fenBefore,
        string from,
        string to,
        string? promotion)
    {
        try
        {
            var result = rules.ValidateAndApply(
                ChessPosition.FromFen(fenBefore),
                new ChessMove(from, to, string.IsNullOrWhiteSpace(promotion) ? null : promotion));

            if (!result.IsLegal) return "";
            if (result.IsCheckmate) return "#";
            return result.IsCheck ? "+" : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    // Square -> (row, file) where row 0 is rank 8, matching ChessPositionAnalyzer.ParseBoard.
    private static (int Row, int File) ToIndex(string square)
        => (8 - (square[1] - '0'), square[0] - 'a');

    private static string FromIndex(int row, int file)
        => $"{FileLetter(file)}{8 - row}";

    private static char FileLetter(int file) => (char)('a' + file);
}
