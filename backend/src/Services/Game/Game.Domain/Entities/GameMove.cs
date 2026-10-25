namespace Game.Domain.Entities;

public sealed class GameMove
{
    public Guid Id { get; private set; }

    public Guid GameId { get; private set; }

    public Guid PlayerId { get; private set; }

    public int MoveNumber { get; private set; }

    public string From { get; private set; } = null!;

    public string To { get; private set; } = null!;

    public string? Promotion { get; private set; }

    public string FenAfterMove { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    private GameMove()
    {
    }

    private GameMove(
        Guid id,
        Guid gameId,
        Guid playerId,
        int moveNumber,
        string from,
        string to,
        string? promotion,
        string fenAfterMove)
    {
        Id = id;
        GameId = gameId;
        PlayerId = playerId;
        MoveNumber = moveNumber;
        From = from;
        To = to;
        Promotion = promotion;
        FenAfterMove = fenAfterMove;
        CreatedAt = DateTime.UtcNow;
    }

    public static GameMove Create(
        Guid gameId,
        Guid playerId,
        int moveNumber,
        string from,
        string to,
        string? promotion,
        string fenAfterMove)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Player ID is required.",
                nameof(playerId));
        }

        if (moveNumber <= 0)
        {
            throw new ArgumentException(
                "Move number must be greater than zero.",
                nameof(moveNumber));
        }

        if (string.IsNullOrWhiteSpace(from))
        {
            throw new ArgumentException(
                "Move origin is required.",
                nameof(from));
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException(
                "Move destination is required.",
                nameof(to));
        }

        if (string.IsNullOrWhiteSpace(fenAfterMove))
        {
            throw new ArgumentException(
                "FEN after the move is required.",
                nameof(fenAfterMove));
        }

        return new GameMove(
            Guid.NewGuid(),
            gameId,
            playerId,
            moveNumber,
            from.Trim(),
            to.Trim(),
            string.IsNullOrWhiteSpace(promotion)
                ? null
                : promotion.Trim(),
            fenAfterMove.Trim());
    }

    public static int CalculateMoveNumber(
    int existingMoveCount)
    {
        if (existingMoveCount < 0)
            throw new ArgumentException(
                "Move count cannot be negative.",
                nameof(existingMoveCount));

        return existingMoveCount + 1;
    }
}