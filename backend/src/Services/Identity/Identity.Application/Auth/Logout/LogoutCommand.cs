namespace Identity.Application.Auth.Logout;

public sealed record LogoutCommand(
    string RefreshToken);