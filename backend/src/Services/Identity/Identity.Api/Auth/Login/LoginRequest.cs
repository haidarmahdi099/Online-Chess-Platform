namespace Identity.Api.Auth.Login;

public sealed record LoginRequest(
    string UsernameOrEmail,
    string Password);