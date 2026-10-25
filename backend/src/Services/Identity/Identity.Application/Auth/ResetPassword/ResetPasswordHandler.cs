using Identity.Application.Abstractions;
using Identity.Application.Common;
using Identity.Domain.Repositories;

namespace Identity.Application.Auth.ResetPassword;

public sealed class ResetPasswordHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenService passwordResetTokenService,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository =
            passwordResetTokenRepository;
        _passwordResetTokenService =
            passwordResetTokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var email =
            command.Email.Trim().ToLowerInvariant();

        var token =
            command.Token.Trim();

        PasswordValidator.Validate(
            command.NewPassword);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(token))
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_reset_request",
                "The password reset request is invalid.");
        }

        var user =
            await _userRepository.GetByEmailAsync(
                email,
                cancellationToken);

        if (user is null)
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_reset_token",
                "The password reset link is invalid or expired.");
        }

        var tokenHash =
            _passwordResetTokenService.HashToken(token);

        var resetToken =
            await _passwordResetTokenRepository
                .GetByTokenHashAsync(
                    tokenHash,
                    cancellationToken);

        if (resetToken is null ||
            resetToken.UserId != user.Id ||
            !resetToken.IsActive())
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_reset_token",
                "The password reset link is invalid or expired.");
        }

        var passwordHash =
            _passwordHasher.Hash(
                command.NewPassword);

        user.ChangePassword(passwordHash);
        user.ClearLoginLockout();

        var completed = await _passwordResetTokenRepository.TryCompleteResetAsync(
            user,
            resetToken.Id,
            cancellationToken);
        if (!completed)
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_reset_token",
                "The password reset link is invalid or expired.");
        }
    }
}
