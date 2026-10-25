namespace Identity.Application.Auth.Login;

public sealed record LoginUserResult(
    Guid UserId,
    string Username,
    string AccessToken,
    string RefreshToken);