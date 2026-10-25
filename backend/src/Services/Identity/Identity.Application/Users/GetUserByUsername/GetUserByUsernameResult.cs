namespace Identity.Application.Users.GetUserByUsername;

public sealed record GetUserByUsernameResult(
    Guid UserId,
    string Username,
    DateTime CreatedAt);