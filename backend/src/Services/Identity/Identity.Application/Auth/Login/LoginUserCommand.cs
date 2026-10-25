namespace Identity.Application.Auth.Login;

public sealed record LoginUserCommand(
    string UsernameOrEmail,
    string Password,
     string IpAddress);