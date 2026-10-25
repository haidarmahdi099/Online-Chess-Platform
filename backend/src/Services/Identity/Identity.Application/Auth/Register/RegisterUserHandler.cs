using Identity.Application.Abstractions;
using Identity.Application.Common;
using Identity.Application.Auth.VerifyEmail;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Identity.Application.Auth.Register;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RegisterUserHandler> _logger;
    private readonly IConfiguration _configuration;


    public RegisterUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher, IEmailSender emailSender, ILogger<RegisterUserHandler> logger,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender; _logger = logger; _configuration = configuration;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<RegisterUserResult> HandleAsync(
    RegisterUserCommand command,
    CancellationToken cancellationToken = default)
    {
        var username = command.Username.Trim();
        var email = command.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new Identity.Application.Common.ApplicationException(
                "username_required",
                "Username is required.");
        }

        if (username.Length is < 3 or > 20 ||
            !Regex.IsMatch(username, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
        {
            throw new Identity.Application.Common.ApplicationException(
                "username_invalid",
                "Username must be 3-20 characters and contain only letters, digits, or underscores.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new Identity.Application.Common.ApplicationException(
                "email_required",
                "Email is required.");
        }

        if (email.Length > 255 || !IsValidEmail(email))
        {
            throw new Identity.Application.Common.ApplicationException(
                "email_invalid",
                "A valid email address of at most 255 characters is required.");
        }

        PasswordValidator.Validate(command.Password);

        if (await _userRepository.ExistsByUsernameAsync(
                username,
                cancellationToken))
        {
            throw new Identity.Application.Common.ApplicationException(
                "username_taken",
                "Username is already taken.");
        }

        if (await _userRepository.ExistsByEmailAsync(
                email,
                cancellationToken))
        {
            throw new Identity.Application.Common.ApplicationException(
                "email_taken",
                "Email is already registered.");
        }

        var passwordHash = _passwordHasher.Hash(
            command.Password);

        var user = User.Create(username, email, passwordHash);
        var verificationCode = EmailVerificationCode.Generate();
        user.SetEmailVerification(
            EmailVerificationCode.Hash(user.Email, verificationCode, _configuration["Jwt:SecretKey"] ?? string.Empty),
            DateTime.UtcNow.Add(EmailVerificationCode.Lifetime));

        await _userRepository.AddAsync(user, cancellationToken);
        try
        {
            await _emailSender.SendEmailAsync(user.Email, "Your ChessPlatform verification code",
                EmailVerificationCode.CreateEmailHtml(verificationCode), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email for user {UserId}; the user can request another verification email.", user.Id);
        }

        return new RegisterUserResult(user.Id, user.Username, user.Email, true);
    }
}
