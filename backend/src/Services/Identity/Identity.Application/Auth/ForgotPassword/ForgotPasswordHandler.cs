using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenService passwordResetTokenService,
        IEmailSender emailSender,
        ILogger<ForgotPasswordHandler> logger)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository =
            passwordResetTokenRepository;
        _passwordResetTokenService =
            passwordResetTokenService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task HandleAsync(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var user =
            await _userRepository.GetByEmailAsync(
                email,
                cancellationToken);

        // Important:
        // We deliberately do nothing if the account
        // does not exist.
        if (user is null)
        {
            return;
        }

        // Invalidate any previous active reset tokens.
        await _passwordResetTokenRepository
            .InvalidateActiveTokensAsync(
                user.Id,
                cancellationToken);

        // Generate the raw token that will be sent
        // to the user's email.
        var rawToken =
            _passwordResetTokenService.GenerateToken();

        // Only the hash is stored in the database.
        var tokenHash =
            _passwordResetTokenService.HashToken(
                rawToken);

        var resetToken =
            PasswordResetToken.Create(
                user.Id,
                tokenHash,
                DateTime.UtcNow.AddMinutes(30));

        await _passwordResetTokenRepository.AddAsync(
            resetToken,
            cancellationToken);

        try
        {
            await _emailSender.SendPasswordResetEmailAsync(
                user.Email,
                rawToken,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Never log the reset token or email contents.
            _logger.LogError(
                exception,
                "Failed to send password reset email for user {UserId}.",
                user.Id);
        }
    }
}
