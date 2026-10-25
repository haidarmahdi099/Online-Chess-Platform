namespace Identity.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(
        Guid userId,
        string username,
        string role);

    string CreateRefreshToken();
}