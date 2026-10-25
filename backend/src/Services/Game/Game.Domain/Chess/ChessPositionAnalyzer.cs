namespace Game.Domain.Chess;

/// <summary>
/// FEN-based position analysis that does not depend on the chess library: check detection,
/// dead-position (insufficient material) detection, halfmove/fullmove clock bookkeeping and
/// repetition keys. The chess library stays responsible only for move legality.
/// </summary>
public static class ChessPositionAnalyzer
{
    // Board is indexed [row, file]; row 0 is rank 8 (the first FEN rank), file 0 is the a-file.
    public static char[,] ParseBoard(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException("FEN is required.", nameof(fen));

        var placement = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var rows = placement.Split('/');
        if (rows.Length != 8)
            throw new ArgumentException("FEN must contain eight ranks.", nameof(fen));

        var board = new char[8, 8];
        for (var r = 0; r < 8; r++)
        {
            var file = 0;
            foreach (var c in rows[r])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }

                if (file > 7)
                    throw new ArgumentException("FEN rank is too long.", nameof(fen));

                board[r, file++] = c;
            }

            if (file != 8)
                throw new ArgumentException("FEN rank must describe eight files.", nameof(fen));
        }

        return board;
    }

    /// <summary>True when the side to move is in check.</summary>
    public static bool IsInCheck(string fen)
    {
        var parts = Split(fen);
        var whiteToMove = parts.Length > 1 && parts[1] == "w";
        var board = ParseBoard(fen);
        var king = whiteToMove ? 'K' : 'k';

        for (var r = 0; r < 8; r++)
        for (var f = 0; f < 8; f++)
        {
            if (board[r, f] == king)
                return IsSquareAttacked(board, r, f, byWhite: !whiteToMove);
        }

        return false;
    }

    /// <summary>
    /// True when neither side can ever deliver checkmate: K vs K, K+minor vs K, or only bishops
    /// that all stand on squares of the same colour.
    /// </summary>
    public static bool IsInsufficientMaterial(string fen)
    {
        var board = ParseBoard(fen);
        var minors = 0;
        var bishopsOnly = true;
        var bishopSquareColor = -1;

        for (var r = 0; r < 8; r++)
        for (var f = 0; f < 8; f++)
        {
            var piece = char.ToUpperInvariant(board[r, f]);
            switch (piece)
            {
                case '\0':
                case 'K':
                    break;
                case 'P':
                case 'R':
                case 'Q':
                    return false;
                case 'N':
                    minors++;
                    bishopsOnly = false;
                    break;
                case 'B':
                    minors++;
                    var color = (r + f) % 2;
                    if (bishopSquareColor == -1) bishopSquareColor = color;
                    else if (bishopSquareColor != color) bishopsOnly = false;
                    break;
            }
        }

        if (minors <= 1) return true;
        return bishopsOnly;
    }

    /// <summary>
    /// Whether the given side can never checkmate the opponent (used to turn a lost-on-time
    /// result into a draw). A lone king can never mate; king + one minor piece can only mate if
    /// the opponent still has other pieces that could block their own king.
    /// </summary>
    public static bool CannotWin(string fen, bool white)
    {
        var board = ParseBoard(fen);
        var minors = 0;
        var opponentHasMaterial = false;

        for (var r = 0; r < 8; r++)
        for (var f = 0; f < 8; f++)
        {
            var c = board[r, f];
            if (c == '\0' || char.ToUpperInvariant(c) == 'K') continue;

            var isWhitePiece = char.IsUpper(c);
            if (isWhitePiece != white)
            {
                opponentHasMaterial = true;
                continue;
            }

            switch (char.ToUpperInvariant(c))
            {
                case 'P':
                case 'R':
                case 'Q':
                    return false;
                default:
                    minors++;
                    break;
            }
        }

        if (minors == 0) return true;
        if (minors == 1) return !opponentHasMaterial;
        return false;
    }

    public static int HalfmoveClock(string fen)
    {
        var parts = Split(fen);
        return parts.Length > 4 && int.TryParse(parts[4], out var value) && value >= 0 ? value : 0;
    }

    /// <summary>
    /// Repetition key: piece placement, side to move, castling rights and an en-passant square
    /// that is only kept when a pawn could actually capture there (FIDE definition).
    /// </summary>
    public static string PositionKey(string fen)
    {
        var parts = Split(fen);
        if (parts.Length < 2) return fen.Trim();

        var castling = parts.Length > 2 ? parts[2] : "-";
        var enPassant = parts.Length > 3 ? parts[3] : "-";
        if (!EnPassantCapturePossible(parts[0], parts[1], enPassant))
            enPassant = "-";

        return $"{parts[0]} {parts[1]} {castling} {enPassant}";
    }

    /// <summary>
    /// Returns the FEN after a move with the halfmove and fullmove counters recomputed from the
    /// previous position, so fifty-move detection never depends on the library's counters.
    /// </summary>
    public static string NormalizeClocks(string previousFen, string newFen, string fromSquare)
    {
        var previous = Split(previousFen);
        var next = Split(newFen);
        if (next.Length < 2)
            throw new InvalidOperationException("The resulting FEN is invalid.");

        var previousBoard = ParseBoard(previousFen);
        var nextBoard = ParseBoard(newFen);

        var file = fromSquare[0] - 'a';
        var row = 7 - (fromSquare[1] - '1');
        var moved = previousBoard[row, file];
        var isPawnMove = moved == 'P' || moved == 'p';
        var isCapture = CountPieces(nextBoard) < CountPieces(previousBoard);

        var halfmove = isPawnMove || isCapture ? 0 : HalfmoveClock(previousFen) + 1;

        var previousFullmove = previous.Length > 5 &&
                               int.TryParse(previous[5], out var parsed) &&
                               parsed >= 1
            ? parsed
            : 1;
        var blackMoved = previous.Length > 1 && previous[1] == "b";
        var fullmove = blackMoved ? previousFullmove + 1 : previousFullmove;

        var castling = next.Length > 2 ? next[2] : "-";
        var enPassant = next.Length > 3 ? next[3] : "-";
        return $"{next[0]} {next[1]} {castling} {enPassant} {halfmove} {fullmove}";
    }

    private static bool EnPassantCapturePossible(string placement, string sideToMove, string enPassant)
    {
        if (enPassant.Length != 2 || enPassant == "-") return false;

        var file = enPassant[0] - 'a';
        var rank = enPassant[1] - '1';
        if (file < 0 || file > 7 || rank < 0 || rank > 7) return false;

        var board = ParseBoard(placement + " w");
        var targetRow = 7 - rank;
        // White captures from the row below the target (higher row index); black from above.
        var pawnRow = sideToMove == "w" ? targetRow + 1 : targetRow - 1;
        var pawn = sideToMove == "w" ? 'P' : 'p';
        if (pawnRow < 0 || pawnRow > 7) return false;

        foreach (var df in new[] { -1, 1 })
        {
            var pawnFile = file + df;
            if (pawnFile is >= 0 and <= 7 && board[pawnRow, pawnFile] == pawn)
                return true;
        }

        return false;
    }

    private static int CountPieces(char[,] board)
    {
        var count = 0;
        for (var r = 0; r < 8; r++)
        for (var f = 0; f < 8; f++)
            if (board[r, f] != '\0') count++;
        return count;
    }

    private static string[] Split(string fen) =>
        fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsSquareAttacked(char[,] board, int row, int file, bool byWhite)
    {
        // Pawns: a white pawn attacks towards row 0, a black pawn towards row 7.
        var pawnRow = byWhite ? row + 1 : row - 1;
        var pawn = byWhite ? 'P' : 'p';
        foreach (var df in new[] { -1, 1 })
        {
            var pf = file + df;
            if (pawnRow is >= 0 and <= 7 && pf is >= 0 and <= 7 && board[pawnRow, pf] == pawn)
                return true;
        }

        // Knights.
        var knight = byWhite ? 'N' : 'n';
        foreach (var (dr, df) in KnightOffsets)
        {
            var r = row + dr;
            var f = file + df;
            if (r is >= 0 and <= 7 && f is >= 0 and <= 7 && board[r, f] == knight)
                return true;
        }

        // Adjacent king.
        var king = byWhite ? 'K' : 'k';
        for (var dr = -1; dr <= 1; dr++)
        for (var df = -1; df <= 1; df++)
        {
            if (dr == 0 && df == 0) continue;
            var r = row + dr;
            var f = file + df;
            if (r is >= 0 and <= 7 && f is >= 0 and <= 7 && board[r, f] == king)
                return true;
        }

        // Sliding pieces.
        var bishop = byWhite ? 'B' : 'b';
        var rook = byWhite ? 'R' : 'r';
        var queen = byWhite ? 'Q' : 'q';

        foreach (var (dr, df) in DiagonalDirections)
            if (SliderAttacks(board, row, file, dr, df, bishop, queen))
                return true;

        foreach (var (dr, df) in OrthogonalDirections)
            if (SliderAttacks(board, row, file, dr, df, rook, queen))
                return true;

        return false;
    }

    private static bool SliderAttacks(
        char[,] board, int row, int file, int dr, int df, char a, char b)
    {
        var r = row + dr;
        var f = file + df;
        while (r is >= 0 and <= 7 && f is >= 0 and <= 7)
        {
            var piece = board[r, f];
            if (piece != '\0')
                return piece == a || piece == b;

            r += dr;
            f += df;
        }

        return false;
    }

    private static readonly (int, int)[] KnightOffsets =
    {
        (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1)
    };

    private static readonly (int, int)[] DiagonalDirections = { (-1, -1), (-1, 1), (1, -1), (1, 1) };
    private static readonly (int, int)[] OrthogonalDirections = { (-1, 0), (1, 0), (0, -1), (0, 1) };
}
