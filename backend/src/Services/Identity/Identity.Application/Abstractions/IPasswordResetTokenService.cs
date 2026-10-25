namespace Identity.Application.Abstractions;

public interface IPasswordResetTokenService
{
    string GenerateToken();

    string HashToken(string token);
}