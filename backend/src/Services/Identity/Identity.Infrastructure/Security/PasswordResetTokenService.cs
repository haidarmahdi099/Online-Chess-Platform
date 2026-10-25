using Identity.Application.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Infrastructure.Security;

public sealed class PasswordResetTokenService
    : IPasswordResetTokenService
{
    public string GenerateToken()
    {
        var randomBytes =
            RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(
            randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }
}