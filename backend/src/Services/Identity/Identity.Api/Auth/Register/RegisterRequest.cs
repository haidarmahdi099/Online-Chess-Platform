namespace Identity.Api.Auth.Register;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password);