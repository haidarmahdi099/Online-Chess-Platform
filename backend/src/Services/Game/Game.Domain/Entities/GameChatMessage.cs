namespace Game.Domain.Entities;

public sealed class GameChatMessage
{
    public Guid Id { get; private set; }

    public Guid GameId { get; private set; }

    public Guid UserId { get; private set; }

    public string Message { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    private GameChatMessage()
    {
    }

    private GameChatMessage(
        Guid id,
        Guid gameId,
        Guid userId,
        string message)
    {
        Id = id;
        GameId = gameId;
        UserId = userId;
        Message = message;
        CreatedAt = DateTime.UtcNow;
    }

    public static GameChatMessage Create(
        Guid gameId,
        Guid userId,
        string message)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Message is required.",
                nameof(message));
        }

        var normalizedMessage = message.Trim();

        if (normalizedMessage.Length > 500)
        {
            throw new ArgumentException(
                "Message cannot exceed 500 characters.",
                nameof(message));
        }

        return new GameChatMessage(
            Guid.NewGuid(),
            gameId,
            userId,
            normalizedMessage);
    }
}