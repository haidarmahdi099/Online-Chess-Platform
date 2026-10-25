namespace Identity.Application.Auth.Register;

public sealed record RegisterUserResult(
    Guid UserId,
    string Username,
    string Email,
    bool EmailVerificationRequired = true);
