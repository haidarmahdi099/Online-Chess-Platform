namespace Game.Domain.Enums;

public enum GameEndReason
{
    Checkmate = 0,
    Resignation = 1,
    DrawAgreement = 2,
    Timeout = 3,
    Stalemate = 4,
    ThreefoldRepetition = 5,
    FiftyMoveRule = 6,
    InsufficientMaterial = 7
}