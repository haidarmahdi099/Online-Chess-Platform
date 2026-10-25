namespace Identity.Application.Users.GetCurrentUser;

public sealed record GetCurrentUserResult(
    Guid UserId,
    string Username,
    string Email,
    DateTime CreatedAt,
    string Role = "User");