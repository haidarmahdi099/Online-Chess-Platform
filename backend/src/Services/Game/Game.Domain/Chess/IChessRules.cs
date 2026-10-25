namespace Game.Domain.Chess;

public interface IChessRules
{
    ChessMoveResult ValidateAndApply(
        ChessPosition position,
        ChessMove move);
}