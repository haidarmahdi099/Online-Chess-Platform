namespace Identity.Application.Auth.Refresh;

public sealed record RefreshTokenResult(
    string AccessToken,
    string RefreshToken);