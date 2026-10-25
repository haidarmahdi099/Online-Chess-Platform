using System.Security.Cryptography;
using System.Text;

namespace Identity.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Token { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string token,
        DateTime createdAt,
        DateTime expiresAt)
    {
        Id = id;
        UserId = userId;
        Token = token;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static RefreshToken Create(
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        return new RefreshToken(
            Guid.NewGuid(),
            userId,
            HashToken(token),
            DateTime.UtcNow,
            expiresAt);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool IsActive()
    {
        return RevokedAt is null &&
               ExpiresAt > DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }
}