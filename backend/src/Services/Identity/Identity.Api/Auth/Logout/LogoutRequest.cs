namespace Identity.Api.Auth.Logout;

public sealed record LogoutRequest(
    string RefreshToken);