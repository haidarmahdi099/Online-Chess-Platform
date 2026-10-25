namespace Identity.Application.Auth.Register;

public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string Password);