using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;

namespace Identity.Application.Auth.Login;

public sealed class LoginUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILoginAttemptRepository _loginAttemptRepository;

    public LoginUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ILoginAttemptRepository loginAttemptRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _loginAttemptRepository = loginAttemptRepository;
    }

    public async Task<LoginUserResult> HandleAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var usernameOrEmail = command.UsernameOrEmail.Trim();
        var ipAddress = command.IpAddress.Trim();

        if (string.IsNullOrWhiteSpace(usernameOrEmail) ||
            string.IsNullOrWhiteSpace(command.Password) ||
            string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_credentials",
                "Invalid email or password, or too many attempts. Try again later.");
        }

        var loginAttempt =
            await _loginAttemptRepository.GetByIpAddressAsync(
                ipAddress,
                cancellationToken);

        if (loginAttempt is not null &&
            loginAttempt.IsLockedOut())
        {
            throw new Identity.Application.Common.ApplicationException(
                "too_many_attempts",
                "Invalid email or password, or too many attempts. Try again later.");
        }

        var user = await _userRepository.GetByUsernameAsync(
            usernameOrEmail,
            cancellationToken);

        if (user is null)
        {
            user = await _userRepository.GetByEmailAsync(
                usernameOrEmail.ToLowerInvariant(),
                cancellationToken);
        }

        if (user is not null &&
            user.IsLockedOut())
        {
            throw new Identity.Application.Common.ApplicationException(
                "too_many_attempts",
                "Invalid email or password, or too many attempts. Try again later.");
        }

        if (user is null ||
            !_passwordHasher.Verify(
                command.Password,
                user.PasswordHash))
        {
            if (user is not null)
            {
                user.RecordFailedLogin();

                await _userRepository.UpdateAsync(
                    user,
                    cancellationToken);
            }

            if (loginAttempt is null)
            {
                loginAttempt = LoginAttempt.Create(ipAddress);

                loginAttempt.RecordFailedAttempt();

                await _loginAttemptRepository.AddAsync(
                    loginAttempt,
                    cancellationToken);
            }
            else
            {
                loginAttempt.RecordFailedAttempt();

                await _loginAttemptRepository.UpdateAsync(
                    loginAttempt,
                    cancellationToken);
            }

            throw new Identity.Application.Common.ApplicationException(
                "invalid_credentials",
                "Invalid email or password, or too many attempts. Try again later.");
        }

        if (!user.EmailVerified)
        {
            throw new Identity.Application.Common.ApplicationException(
                "email_not_verified", "Verify your email address before signing in.");
        }

        user.RecordSuccessfulLogin();

        await _userRepository.UpdateAsync(
            user,
            cancellationToken);

        if (loginAttempt is not null)
        {
            loginAttempt.RecordSuccessfulAttempt();

            await _loginAttemptRepository.UpdateAsync(
                loginAttempt,
                cancellationToken);
        }

        var accessToken = _tokenService.CreateAccessToken(
            user.Id,
            user.Username,
            user.Role);

        var refreshTokenValue =
            _tokenService.CreateRefreshToken();

        var refreshToken = RefreshToken.Create(
            user.Id,
            refreshTokenValue,
            DateTime.UtcNow.AddDays(30));

        await _refreshTokenRepository.AddAsync(
            refreshToken,
            cancellationToken);

        return new LoginUserResult(
            user.Id,
            user.Username,
            accessToken,
            refreshTokenValue);
    }
}