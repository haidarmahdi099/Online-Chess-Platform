using Game.Domain.Chess;
using pax.chess;
using pax.chess.Extensions;

namespace Game.Infrastructure.Chess;

public sealed class PaxChessRules : IChessRules
{
    public ChessMoveResult ValidateAndApply(
    ChessPosition position,
    ChessMove move)
    {
        var boardPosition = FenSerializer.Parse(position.Fen);

        var game = new ChessGame(boardPosition);

        var uci = $"{move.From}{move.To}{move.Promotion ?? ""}";

        var libraryMove = Uci.CreateMove(
            uci,
            game.CurrentPosition);

        if (libraryMove is null)
        {
            return new ChessMoveResult(
                false,
                "invalid_move",
                "The requested move is invalid.",
                null,
                false,
                false,
                false);
        }

        var moveState = game.ApplyMove(libraryMove);

        if (moveState != MoveState.Ok)
        {
            return new ChessMoveResult(
                false,
                "illegal_move",
                "The move is not legal in the current position.",
                null,
                false,
                false,
                false);
        }

        var newFen = FenSerializer.Serialize(game.CurrentPosition);

        game.Evaluate();

        var termination = game.Conclusion?.Termination;

        var isCheckmate = termination == GameTermination.Checkmate;
        var isStalemate = termination == GameTermination.Stalemate;
        // The library result only reports terminal states; detect plain check from the FEN.
        var isCheck = isCheckmate || ChessPositionAnalyzer.IsInCheck(newFen);

        return new ChessMoveResult(
            true,
            null,
            null,
            newFen,
            isCheck,
            isCheckmate,
            isStalemate);
    }
}