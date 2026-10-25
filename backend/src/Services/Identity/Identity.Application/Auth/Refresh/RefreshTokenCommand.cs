namespace Identity.Application.Auth.Refresh;

public sealed record RefreshTokenCommand(
    string RefreshToken);