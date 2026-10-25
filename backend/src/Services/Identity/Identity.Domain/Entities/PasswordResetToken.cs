namespace Identity.Domain.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? UsedAt { get; private set; }

    private PasswordResetToken()
    {
    }

    private PasswordResetToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt)
    {
        return new PasswordResetToken(
            Guid.NewGuid(),
            userId,
            tokenHash,
            DateTime.UtcNow,
            expiresAt);
    }

    public bool IsActive()
    {
        return UsedAt is null &&
               ExpiresAt > DateTime.UtcNow;
    }

    public void MarkAsUsed()
    {
        if (UsedAt is not null)
        {
            return;
        }

        UsedAt = DateTime.UtcNow;
    }
}